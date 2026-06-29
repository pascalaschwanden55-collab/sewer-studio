using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Bekannte Katalog-Codes (einmalig aufgebaut) zur Validierung des vsa_code_hint. Null = keine Validierung moeglich.</summary>
    private readonly IReadOnlySet<string>? _knownCodes;

    public EnhancedVisionAnalysisService(
        OllamaClient client,
        string model,
        ICodeCatalogProvider? codeCatalog = null)
    {
        _client = client;
        _model = model;
        _codeCatalog = codeCatalog;
        _knownCodes = BuildKnownCodeSet(codeCatalog);
    }

    /// <summary>
    /// Baut die Menge der im aktiven Katalog bekannten Codes.
    /// Delegiert an <see cref="EnhancedVisionPromptBuilder.BuildKnownCodeSet"/>.
    /// </summary>
    internal static IReadOnlySet<string>? BuildKnownCodeSet(ICodeCatalogProvider? catalog)
        => EnhancedVisionPromptBuilder.BuildKnownCodeSet(catalog);

    /// <summary>
    /// Validiert einen LLM-Code-Hint gegen den Katalog.
    /// Delegiert an <see cref="EnhancedVisionPromptBuilder.ValidateCodeHint"/>.
    /// </summary>
    internal static string? ValidateCodeHint(string? hint, IReadOnlySet<string>? knownCodes)
        => EnhancedVisionPromptBuilder.ValidateCodeHint(hint, knownCodes);

    /// <summary>
    /// Normalisiert die vom LLM gelieferte BBox [x1,y1,x2,y2] (0-1).
    /// Delegiert an <see cref="EnhancedVisionPromptBuilder.NormalizeBbox"/>.
    /// </summary>
    internal static (double? X1, double? Y1, double? X2, double? Y2) NormalizeBbox(IReadOnlyList<double>? bbox)
        => EnhancedVisionPromptBuilder.NormalizeBbox(bbox);

    // Standard Per-Frame-Qwen-Cap (#9): 120s. Dies ist der real wirksame Cap auch im
    // Multi-Model-Pfad (innerer LinkedTokenSource gewinnt gegen den aeusseren QwenFrameTimeout).
    // Ein separates groesseres Budget fuers 32B-Modell (z.B. 300s) erfordert, diesen Cap pro
    // Instanz konfigurierbar zu machen — bewusst spaeterer Folgeschritt.
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(120);

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

            dto = await _client.ChatStructuredWithOptionsAsync<EnhancedVisionDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                options: OllamaDeterministicOptions.Create(),
                ct: frameCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return EnhancedFrameAnalysis.Empty(
                $"Timeout ({FrameTimeout.TotalSeconds:0}s)",
                AnalysisOutcome.Timeout);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.EmptyFromException(ex);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Baut den vollstaendigen Analyse-Prompt.
    /// Delegiert an <see cref="EnhancedVisionPromptBuilder.BuildPrompt"/>.
    /// </summary>
    private string BuildPrompt(
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext = null,
        IReadOnlyList<string>? observationHints = null)
        => EnhancedVisionPromptBuilder.BuildPrompt(_codeCatalog, importContext, observationHints);

    /// <summary>
    /// Baut den VSA-KEK-Katalogauszug fuer den Prompt.
    /// Delegiert an <see cref="EnhancedVisionPromptBuilder.BuildDamageClassesPrompt"/>.
    /// </summary>
    internal static string BuildDamageClassesPrompt(ICodeCatalogProvider? codeCatalog)
        => EnhancedVisionPromptBuilder.BuildDamageClassesPrompt(codeCatalog);

    private EnhancedFrameAnalysis MapToAnalysis(EnhancedVisionDto dto)
    {
        var findings = (dto.Findings ?? Array.Empty<EnhancedFindingDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Label))
            .Select(f =>
            {
                // BBox parsen + normalisieren: [x1, y1, x2, y2] normalisiert (0-1)
                var (bx1, by1, bx2, by2) = NormalizeBbox(f.Bbox);

                return new EnhancedFinding(
                    Label: f.Label.Trim(),
                    VsaCodeHint: ValidateCodeHint(f.VsaCodeHint, _knownCodes),
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
            // Plausibilitaet: 0..500 m; fehlgelesene Knotennummern (5+ stellig) -> null,
            // damit kein halluzinierter Meter in die Timeline laeuft. (Audit R7)
            Meter: MeterPlausibility.Sanitize(dto.Meter),
            PipeMaterial: dto.PipeMaterial ?? "unbekannt",
            PipeDiameterMm: dto.PipeDiameterMm,
            Findings: findings,
            ImageQuality: dto.ImageQuality ?? "mittel",
            IsEmptyFrame: dto.IsEmptyFrame,
            Error: null,
            Outcome: findings.Count == 0 || dto.IsEmptyFrame
                ? AnalysisOutcome.NoFinding
                : AnalysisOutcome.Ok);
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

            dto = await _client.ChatStructuredWithOptionsAsync<EnhancedVisionDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                options: OllamaDeterministicOptions.Create(),
                ct: frameCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return EnhancedFrameAnalysis.Empty(
                $"Timeout ({FrameTimeout.TotalSeconds:0}s)",
                AnalysisOutcome.Timeout);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.EmptyFromException(ex);
        }

        return MapToAnalysis(dto);
    }

    private static string BuildContextPrompt(MultiModelFrameResult ctx, int pipeDiameterMm,
        (string Code, string Description, double Meter, double Confidence)? previousFinding = null)
        => EnhancedVisionPromptBuilder.BuildContextPrompt(ctx, pipeDiameterMm, previousFinding);

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

