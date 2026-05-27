using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Verbesserte Vision-Analyse mit vollständiger Schadensklassenliste nach
/// DIN EN 13508-2 und VSA-DSS (umfangreiche Schadensklassifikation).
///
/// Unterschiede zum bestehenden OllamaVisionFindingsService:
/// - Detailliertere Schadensklassen (Typ + Untertyp + Lage)
/// - Quantifizierung direkt im Vision-Schritt (Severity 1-5)
/// - OSD-Erkennung für Meterstand, Zeit, Haltungsinfo
/// - Materialkennzeichnung
/// - Strukturierte Ausgabe mit vsaCode-Vorschlag
/// </summary>
public sealed class EnhancedVisionAnalysisService
{
    private static readonly JsonElement EnhancedVisionSchema = JsonDocument.Parse("""
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "meter": { "type": ["number", "null"], "description": "OSD-Meterstand in Metern" },
        "time_in_video": { "type": ["number", "null"], "description": "Zeitstempel im Video in Sekunden" },
        "pipe_material": {
          "type": "string",
          "enum": ["beton", "steinzeug", "pvc", "pe", "gfk", "stahl", "unbekannt"]
        },
        "pipe_diameter_mm": { "type": ["integer", "null"] },
        "findings": {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "properties": {
              "label": { "type": "string" },
              "vsa_code_hint": { "type": ["string", "null"], "description": "Wahrscheinlichster VSA/EN-Code" },
              "severity": { "type": "integer", "minimum": 1, "maximum": 5 },
              "position_clock": { "type": ["string", "null"], "description": "Uhrzeitlage z.B. 12:00, 6:00, 3:00" },
              "extent_percent": { "type": ["integer", "null"], "description": "Ausdehnung in % des Umfangs" },
              "height_mm": { "type": ["integer", "null"], "description": "Schadenshöhe in mm" },
              "width_mm": { "type": ["integer", "null"], "description": "Schadensbreite in mm" },
              "intrusion_percent": { "type": ["integer", "null"], "description": "Einragungsgrad in %" },
              "cross_section_reduction_percent": { "type": ["integer", "null"], "description": "Querschnittsverringerung in %" },
              "diameter_reduction_mm": { "type": ["integer", "null"], "description": "Durchmesserverringerung in mm" },
              "bbox": { "type": ["array", "null"], "description": "Bounding Box [x1, y1, x2, y2] normalisiert 0-1, linke obere und rechte untere Ecke der Schadensregion im Bild", "items": { "type": "number" } },
              "notes": { "type": ["string", "null"] }
            },
            "required": ["label", "severity"]
          }
        },
        "image_quality": {
          "type": "string",
          "enum": ["gut", "mittel", "schlecht"]
        },
        "is_empty_frame": { "type": "boolean" }
      },
      "required": ["meter", "findings", "image_quality", "is_empty_frame"]
    }
    """).RootElement.Clone();

    private readonly OllamaClient _client;
    private readonly string _model;
    private readonly ICodeCatalogProvider? _codeCatalog;

    public EnhancedVisionAnalysisService(
        OllamaClient client,
        string model,
        ICodeCatalogProvider? codeCatalog = null)
    {
        _client = client;
        _model = model;
        _codeCatalog = codeCatalog;
    }

    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(60);

    public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
        string framePngBase64,
        CancellationToken ct = default)
        => await AnalyzeAsync(framePngBase64, null, ct);

    /// <summary>
    /// Analyse mit Import-Kontext: Bekannte Befunde aus dem Protokoll werden
    /// als Erwartungshorizont in den Prompt injiziert, damit Qwen passende
    /// VSA-Codes zuweisen kann statt "???".
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
        string framePngBase64,
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(importContext);
        return await AnalyzeWithPromptAsync(framePngBase64, prompt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Analyse mit unsicheren Bild-Hinweisen, z.B. aus YOLO-cls.
    /// Diese Hinweise sind bewusst keine VSA-Code-Vorgabe.
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzeWithObservationHintsAsync(
        string framePngBase64,
        IReadOnlyList<string>? observationHints,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(importContext: null, observationHints: observationHints);
        return await AnalyzeWithPromptAsync(framePngBase64, prompt, ct).ConfigureAwait(false);
    }

    private async Task<EnhancedFrameAnalysis> AnalyzeWithPromptAsync(
        string framePngBase64,
        string prompt,
        CancellationToken ct)
    {
        EnhancedVisionDto dto;
        try
        {
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(FrameTimeout);

            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: frameCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return EnhancedFrameAnalysis.Empty("Timeout (60s)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    private string BuildPrompt(
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext = null,
        IReadOnlyList<string>? observationHints = null)
    {
        var contextSection = BuildImportContextSection(importContext);
        var observationHintsSection = BuildObservationHintsSection(observationHints);

        return $"""
Du analysierst einen Frame aus einem Kanalinspektion-Video (TV-Inspektion Abwasserkanal).

AUFGABEN:
1. Lies den METERSTAND aus dem OSD (On-Screen Display) – typisch unten rechts im Bild als Dezimalzahl (z.B. "2.64", "18.40").
   IGNORIERE grosse Zahlen im oberen Header (Knotennummern wie 74468, 80872). Meterstand ist IMMER kleiner als 500.
2. Erkenne das ROHRMATERIAL und den DURCHMESSER falls sichtbar.
3. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
4. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel, "6:00" = Sohle).
5. Beurteile die Bildqualität.
6. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Einragungsgrad (%), Querschnittsverringerung (%), Durchmesserverringerung (mm).
7. Gib fuer jeden Schaden den passenden VSA-Code als vsa_code_hint an.
8. Wenn der exakte Untertyp unklar ist, verwende den passenden HAUPTCODE statt "???".
   Beispiele: Anschluss -> BCA, Bogen -> BCC, Ablagerung -> BBC.
{contextSection}
{observationHintsSection}
{BuildDamageClassesPrompt()}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme
9. Gib fuer jeden Schaden eine bbox an: [x1, y1, x2, y2] normalisiert (0.0=links/oben, 1.0=rechts/unten).
   bbox = Region des Schadens IM BILD, nicht die Rohruhr-Position.

Antworte AUSSCHLIESSLICH mit gültigem JSON gemäß Schema.
Falls kein Schaden erkennbar: findings=[], is_empty_frame=true.
""";
    }

    private string BuildDamageClassesPrompt()
        => BuildDamageClassesPrompt(_codeCatalog);

    internal static string BuildDamageClassesPrompt(ICodeCatalogProvider? codeCatalog)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VSA-KEK-KATALOGAUSZUG (Code-Wahrheit aus aktivem Katalog):");
        sb.AppendLine();

        sb.AppendLine("GRUNDSTRUKTUR DER HALTUNG (HOECHSTE PRIORITAET - immer erkennen!):");
        AppendCodeLine(sb, codeCatalog, "BCD", "Rohranfang", "Schacht sichtbar, Kamera faehrt in das Rohr ein");
        AppendCodeLine(sb, codeCatalog, "BCE", "Rohrende", "Schacht sichtbar am Ende, Kamera erreicht das Rohrende");
        AppendCodeLine(sb, codeCatalog, "BCA", "Seitlicher Anschluss", "seitliche Rohroeffnung in der Kanalwand");
        AppendCodeLine(sb, codeCatalog, "BCAEB", "Anschluss eingespitzt, verschlossen", null);
        AppendCodeLine(sb, codeCatalog, "BAHC", "Anschluss unvollstaendig eingebunden", "Stutzen ragt in den Kanal hinein");
        AppendCodeLine(sb, codeCatalog, "BCC", "Bogen", "Richtungsaenderung des Kanals");
        sb.AppendLine();

        sb.AppendLine("STRUKTURELLE SCHAEDEN:");
        AppendCodeLine(sb, codeCatalog, "BAB", "Riss", "laengs/quer/diagonal/ringfoermig/verzweigt");
        AppendCodeLine(sb, codeCatalog, "BAC", "Bruch", "partiell oder total");
        AppendCodeLine(sb, codeCatalog, "BAF", "Deformation", "vertikal oder horizontal");
        AppendCodeLine(sb, codeCatalog, "BAH", "Versatz", "vertikal oder horizontal");
        AppendCodeLine(sb, codeCatalog, "BAI", "Einragung Stutzen/Anschluss", null);
        sb.AppendLine();

        sb.AppendLine("OBERFLAECHEN / EINWUCHS / ABLAGERUNGEN:");
        AppendCodeLine(sb, codeCatalog, "BAJ", "Ausbrueche/Abplatzungen", null);
        AppendCodeLine(sb, codeCatalog, "BBA", "Wurzeln", "Wurzeleinwuchs/Bewuchs");
        AppendCodeLine(sb, codeCatalog, "BBB", "Anhaftende Stoffe", "Inkrustation/Fett/anhaftende Stoffe");
        AppendCodeLine(sb, codeCatalog, "BBC", "Ablagerungen", "Sand/Kies/verfestigte Ablagerung");
        AppendCodeLine(sb, codeCatalog, "BBD", "Eindringendes Bodenmaterial", null);
        sb.AppendLine();

        sb.AppendLine("SONSTIGES:");
        AppendCodeLine(sb, codeCatalog, "BDDC", "Wasserspiegel/Wasserstand", null);
        AppendCodeLine(sb, codeCatalog, "BABBA", "Riss laengs", "mit Uhrlage und Breite in mm");
        AppendCodeLine(sb, codeCatalog, "BABAA", "Riss quer", null);

        return sb.ToString();
    }

    private static void AppendCodeLine(
        StringBuilder sb,
        ICodeCatalogProvider? codeCatalog,
        string code,
        string fallbackTitle,
        string? hint)
    {
        var title = LookupCatalogTitle(codeCatalog, code) ?? fallbackTitle;
        sb.Append($"- {code} = {title}");
        if (!string.IsNullOrWhiteSpace(hint))
            sb.Append($" ({hint})");
        sb.AppendLine();
    }

    private static string? LookupCatalogTitle(ICodeCatalogProvider? codeCatalog, string code)
    {
        if (codeCatalog is null)
            return null;

        if (codeCatalog.TryGet(code, out var exact) && !string.IsNullOrWhiteSpace(exact.Title))
            return exact.Title.Trim();

        if (code.Length > 3 && codeCatalog.TryGet(code[..3], out var main) && !string.IsNullOrWhiteSpace(main.Title))
            return main.Title.Trim();

        return null;
    }

    /// <summary>
    /// Baut den Import-Kontext-Abschnitt: Bekannte Befunde aus dem Inspektionsprotokoll
    /// als Erwartungshorizont fuer die KI-Analyse.
    /// </summary>
    private static string BuildImportContextSection(
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext)
    {
        if (importContext is null || importContext.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("BEKANNTE BEFUNDE AUS DEM INSPEKTIONSPROTOKOLL (Erwartungshorizont):");
        sb.AppendLine("Diese Schaeden wurden in dieser Haltung bereits dokumentiert.");
        sb.AppendLine("Verwende bevorzugt diese VSA-Codes wenn die visuellen Anzeichen passen:");

        // Deduplizierung: gleicher Code nur einmal zeigen
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, desc, meter) in importContext)
        {
            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                continue;
            var meterInfo = meter > 0 ? $" @ {meter:F1}m" : "";
            sb.AppendLine($"  - {code}: {desc}{meterInfo}");
        }

        sb.AppendLine();
        sb.AppendLine("WICHTIG: Wenn du einen Schaden erkennst der zu einem dieser Codes passt,");
        sb.AppendLine("verwende EXAKT diesen Code als vsa_code_hint (nicht erfinden, nicht ??? verwenden).");
        sb.AppendLine("is_empty_frame=true nur dann setzen, wenn keiner dieser bekannten Befunde sichtbar ist.");
        sb.AppendLine("Bekannte Befunde koennen auch Rohranfang, Rohrende, Wasserstand, Anschluss oder Bogen sein - nicht nur klassische Schaeden.");
        return sb.ToString();
    }

    private static string BuildObservationHintsSection(IReadOnlyList<string>? observationHints)
    {
        if (observationHints is null || observationHints.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("ZUSAETZLICHE BILD-HINWEISE (unsicher, nicht als VSA-Code uebernehmen):");
        foreach (var hint in observationHints)
        {
            if (!string.IsNullOrWhiteSpace(hint))
                sb.AppendLine($"  - {hint.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("Diese Hinweise sind nur ein Suchhinweis. Verwende sie nicht als VSA-Code.");
        sb.AppendLine("is_empty_frame=true nur dann setzen, wenn trotz Hinweis keine sichtbare Auffaelligkeit vorhanden ist.");
        return sb.ToString();
    }

    private static EnhancedFrameAnalysis MapToAnalysis(EnhancedVisionDto dto)
    {
        var findings = (dto.Findings ?? Array.Empty<EnhancedFindingDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Label))
            .Select(f =>
            {
                // BBox parsen: [x1, y1, x2, y2] normalisiert
                double? bx1 = null, by1 = null, bx2 = null, by2 = null;
                if (f.Bbox is { Count: >= 4 })
                {
                    bx1 = Math.Clamp(f.Bbox[0], 0, 1);
                    by1 = Math.Clamp(f.Bbox[1], 0, 1);
                    bx2 = Math.Clamp(f.Bbox[2], 0, 1);
                    by2 = Math.Clamp(f.Bbox[3], 0, 1);
                }

                return new EnhancedFinding(
                    Label: f.Label.Trim(),
                    VsaCodeHint: f.VsaCodeHint?.Trim(),
                    Severity: Math.Clamp(f.Severity, 1, 5),
                    PositionClock: f.PositionClock?.Trim(),
                    ExtentPercent: f.ExtentPercent,
                    HeightMm: f.HeightMm,
                    WidthMm: f.WidthMm,
                    IntrusionPercent: f.IntrusionPercent,
                    CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                    DiameterReductionMm: f.DiameterReductionMm,
                    BboxX1: bx1, BboxY1: by1, BboxX2: bx2, BboxY2: by2,
                    Notes: f.Notes?.Trim());
            })
            .ToList();

        return new EnhancedFrameAnalysis(
            Meter: dto.Meter,
            PipeMaterial: dto.PipeMaterial ?? "unbekannt",
            PipeDiameterMm: dto.PipeDiameterMm,
            Findings: findings,
            ImageQuality: dto.ImageQuality ?? "mittel",
            IsEmptyFrame: dto.IsEmptyFrame,
            Error: null);
    }

    /// <summary>
    /// Enhanced analysis that takes DINO/SAM context to improve VSA code assignment.
    /// The LLM receives bounding-box coordinates and quantification values as context.
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzeWithContextAsync(
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm = 300,
        CancellationToken ct = default,
        (string Code, string Description, double Meter, double Confidence)? previousFinding = null)
    {
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm, previousFinding);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();

        EnhancedVisionDto dto;
        try
        {
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(FrameTimeout);

            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: frameCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return EnhancedFrameAnalysis.Empty("Timeout (60s)");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    private static string BuildContextPrompt(MultiModelFrameResult ctx, int pipeDiameterMm,
        (string Code, string Description, double Meter, double Confidence)? previousFinding = null)
    {
        var sb = new StringBuilder();

        // Vorheriger Befund fuer temporale Kohaerenz
        if (previousFinding is var (prevCode, prevDesc, prevMeter, prevConf))
        {
            sb.AppendLine("VORHERIGER BEFUND (Kontext aus dem vorherigen Analyseabschnitt):");
            sb.AppendLine($"  Bei {prevMeter:F2}m wurde '{prevCode}' ({prevDesc}) vermutet (Konfidenz: {prevConf:F0}%).");
            sb.AppendLine("  Pruefe ob das aktuelle Bild dasselbe Objekt zeigt oder einen neuen/anderen Befund.");
            sb.AppendLine();
        }

        sb.AppendLine("KONTEXT AUS VORHERIGER ANALYSE (Computer Vision Modelle):");
        sb.AppendLine($"- Bild: {ctx.ImageWidth}x{ctx.ImageHeight} px");
        sb.AppendLine($"- Rohrdurchmesser: DN{pipeDiameterMm}");
        sb.AppendLine();

        if (ctx.DinoDetections.Count > 0)
        {
            sb.AppendLine("ERKANNTE OBJEKTE (Grounding DINO):");
            foreach (var det in ctx.DinoDetections)
            {
                sb.AppendLine($"  - {det.Label} (Confidence={det.Confidence:F2}) @ [{det.X1:F0},{det.Y1:F0},{det.X2:F0},{det.Y2:F0}]");
            }
            sb.AppendLine();
        }

        if (ctx.SamMasks.Count > 0)
        {
            var quantified = MaskQuantificationService.QuantifyAll(
                new SamResponse(ctx.SamMasks, ctx.ImageWidth, ctx.ImageHeight, 0),
                pipeDiameterMm);

            sb.AppendLine("SEGMENTIERUNGSERGEBNISSE (SAM – pixelgenaue Masken):");
            foreach (var q in quantified)
            {
                sb.AppendLine($"  - {q.Label}: Höhe={q.HeightMm}mm, Breite={q.WidthMm}mm, " +
                    $"Ausdehnung={q.ExtentPercent}%, Querschnitt={q.CrossSectionReductionPercent}%, " +
                    $"Uhrlage={q.ClockPosition ?? "?"}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Bitte nutze diese Voranalyse als Kontext. Die Quantifizierungswerte aus SAM sind pixelgenau berechnet – übernimm sie bevorzugt.");
        sb.AppendLine("Deine Aufgabe: VSA-Code-Zuweisung und Validierung der Klassifizierung.");
        return sb.ToString();
    }

    // ── DTOs (für JSON-Deserialisierung) ──────────────────────────────────────

    private sealed record EnhancedVisionDto(
        [property: JsonPropertyName("meter")]
        double? Meter,
        [property: JsonPropertyName("time_in_video")]
        double? TimeInVideo,
        [property: JsonPropertyName("pipe_material")]
        string? PipeMaterial,
        [property: JsonPropertyName("pipe_diameter_mm")]
        int? PipeDiameterMm,
        [property: JsonPropertyName("findings")]
        IReadOnlyList<EnhancedFindingDto>? Findings,
        [property: JsonPropertyName("image_quality")]
        string? ImageQuality,
        [property: JsonPropertyName("is_empty_frame")]
        bool IsEmptyFrame);

    private sealed record EnhancedFindingDto(
        [property: JsonPropertyName("label")]
        string Label,
        [property: JsonPropertyName("vsa_code_hint")]
        string? VsaCodeHint,
        [property: JsonPropertyName("severity")]
        int Severity,
        [property: JsonPropertyName("position_clock")]
        string? PositionClock,
        [property: JsonPropertyName("extent_percent")]
        int? ExtentPercent,
        [property: JsonPropertyName("height_mm")]
        int? HeightMm,
        [property: JsonPropertyName("width_mm")]
        int? WidthMm,
        [property: JsonPropertyName("intrusion_percent")]
        int? IntrusionPercent,
        [property: JsonPropertyName("cross_section_reduction_percent")]
        int? CrossSectionReductionPercent,
        [property: JsonPropertyName("diameter_reduction_mm")]
        int? DiameterReductionMm,
        [property: JsonPropertyName("bbox")]
        IReadOnlyList<double>? Bbox,
        [property: JsonPropertyName("notes")]
        string? Notes);
}

// ── Analysis result types ─────────────────────────────────────────────────────

