using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Ai;

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
          "enum": ["beton", "steinzeug", "pvc", "pe", "gfk", "stahl", "mauerwerk", "faserzement", "unbekannt"]
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

    // Vollständige Schadensklassen nach DIN EN 13508-2 / VSA-DSS
    // Gruppiert für besseren Prompt
    private static readonly string DamageClassesPrompt = """
ERKENNBARE SCHADENSKLASSEN mit VSA/EN 13508-2 Codes (VERWENDE DIESE CODES als vsa_code_hint):

BESTANDSAUFNAHME (BC-Gruppe) – Diese Bilder zeigen KEINEN Schaden, aber MÜSSEN erkannt werden:
- Rohranfang: Blick vom Schacht ins Rohr, Schachtwand/Mauerwerk sichtbar → BCD
- Rohrende: Blick auf Endschacht, Schacht am Ende → BCE
- Seitlicher Anschluss: Runde/ovale Öffnung seitlich in Rohrwand → BCA
- Bogen/Kurve: Rohr biegt ab → BCC
- Allgemeine Anmerkung → BDB

STRUKTURELLE SCHÄDEN (BA-Gruppe):
- Riss (längs, quer, diagonal, ringförmig, verzweigt) → BAB
- Bruch (partiell, total) → BAC
- Einsturz/Kollaps → BAD
- Deformation (vertikal, horizontal, Ovalität) → BAF
- Versatz (vertikal, horizontal) → BAH
- Einragender Stutzen/Anschluss → BAI
- Loch/Wanddurchdringung → BAG
- Offene Muffenverbindung → BAE

OBERFLÄCHENSCHÄDEN (BA-Gruppe):
- Korrosion, Ausbrüche, Abplatzungen → BABB (oder BAB wenn unsicher)

BETRIEBLICHE STÖRUNGEN (BB-Gruppe):
- Inkrustation/Kalkablagerung → BBA
- Wurzeleinwuchs → BBB
- Ablagerung (Sand, Schlamm, Kies) → BBC
- Fremdkörper → BBD
- Eindringendes Wasser/Infiltration → BBF

ANSCHLÜSSE:
- Undichter/offener Anschluss → BCA
- Eindringendes Wasser am Anschluss → BCB

WICHTIG: vsa_code_hint MUSS immer ausgefüllt werden wenn ein Befund vorliegt.
Verwende IMMER die oben angegebenen Codes (BCD, BAB, BBC usw.), keine anderen Kürzel.
""";


    private readonly OllamaClient _client;
    private readonly string _model;
    private readonly string? _referenceModel;
    private readonly int _numCtx;
    // 2 parallele Eskalationen erlauben (RTX 5090: genug VRAM fuer 32B + 8B)
    private readonly SemaphoreSlim _escalationLock = new(2, 2);
    private int _escalationCount;
    private FewShotExampleStore? _fewShotStore;
    private IReadOnlyList<(FewShotExample Example, string Base64)>? _cachedFewShot;

    public EnhancedVisionAnalysisService(OllamaClient client, string model, string? referenceModel = null, int numCtx = 4096)
    {
        _client = client;
        _model = model;
        _referenceModel = referenceModel;
        _numCtx = numCtx;
    }

    /// <summary>Anzahl erfolgreicher Eskalationen (Telemetrie).</summary>
    public int EscalationCount => _escalationCount;

    /// <summary>
    /// Aktiviert Few-Shot Learning: Beispielbilder werden in den Prompt injiziert.
    /// Sollte einmal beim Start aufgerufen werden.
    /// </summary>
    public async Task EnableFewShotAsync(FewShotExampleStore store, CancellationToken ct = default)
    {
        _fewShotStore = store;
        await store.LoadAsync(ct);

        // Few-Shot Beispiele basierend auf Context-Limit
        int maxExamples = _numCtx <= 2048 ? 2 : 4;
        var examples = await store.GetBestExamplesAsync(maxExamples, ct: ct);

        // Bilder vorladen und als Base64 cachen (einmal laden, bei jeder Analyse verwenden)
        var loaded = new List<(FewShotExample, string)>();
        foreach (var ex in examples)
        {
            var imgBytes = await store.LoadImageAsync(ex, ct);
            if (imgBytes != null)
                loaded.Add((ex, Convert.ToBase64String(imgBytes)));
        }

        _cachedFewShot = loaded;

        System.Diagnostics.Debug.WriteLine(
            $"[EnhancedVision] Few-Shot aktiviert: {loaded.Count} Beispiele geladen " +
            $"({string.Join(", ", loaded.Select(l => l.Item1.VsaCode))})");
    }

    public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(framePngBase64);

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] KI-Fehler ({_model}): {ex.GetType().Name}: {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Analysiert ein Foto aus einem PDF-Bildbericht (KEIN Video-Frame).
    /// Angepasster Prompt ohne OSD-Erwartung, fokussiert auf Schadenserkennung.
    /// </summary>
    public async Task<EnhancedFrameAnalysis> AnalyzePdfPhotoAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele auch fuer PDF-Fotos nutzen
        if (_cachedFewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in _cachedFewShot)
            {
                var exPrompt = $"Analysiere dieses Kanalbild. " +
                    $"Hinweis: Dieses Bild zeigt {example.Description}" +
                    (example.ClockPosition != null ? $" bei {example.ClockPosition}" : "") +
                    $" (VSA-Code: {example.VsaCode}).";

                messages.Add(new OllamaClient.ChatMessage(
                    Role: "user",
                    Content: exPrompt,
                    ImagesBase64: [b64]));

                var exResponse = BuildFewShotResponse(example);
                messages.Add(new OllamaClient.ChatMessage(
                    Role: "assistant",
                    Content: exResponse));
            }
        }

        messages.Add(new OllamaClient.ChatMessage(
            Role: "user",
            Content: BuildPdfPhotoPrompt(),
            ImagesBase64: [framePngBase64]));

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] PDF-Foto KI-Fehler ({_model}): {ex.GetType().Name}: {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Baut die Chat-Messages auf, mit Few-Shot Beispielen falls vorhanden.
    /// </summary>
    private IReadOnlyList<OllamaClient.ChatMessage> BuildMessages(string framePngBase64)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele als User/Assistant-Paare injizieren
        if (_cachedFewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in _cachedFewShot)
            {
                // User zeigt Beispielbild mit Kontext
                var exPrompt = $"Analysiere dieses Kanalbild. " +
                    $"Hinweis: Dieses Bild zeigt {example.Description}" +
                    (example.ClockPosition != null ? $" bei {example.ClockPosition}" : "") +
                    $" (VSA-Code: {example.VsaCode}).";

                messages.Add(new OllamaClient.ChatMessage(
                    Role: "user",
                    Content: exPrompt,
                    ImagesBase64: [b64]));

                // Assistant antwortet mit korrekter Klassifizierung
                var exResponse = BuildFewShotResponse(example);
                messages.Add(new OllamaClient.ChatMessage(
                    Role: "assistant",
                    Content: exResponse));
            }
        }

        // Eigentliche Analyse-Anfrage
        messages.Add(new OllamaClient.ChatMessage(
            Role: "user",
            Content: BuildPrompt(),
            ImagesBase64: [framePngBase64]));

        return messages;
    }

    /// <summary>Baut eine synthetische "korrekte" Assistant-Antwort fuer ein Few-Shot Beispiel.</summary>
    private static string BuildFewShotResponse(FewShotExample example)
    {
        var finding = new Dictionary<string, object?>
        {
            ["label"] = example.Description.Length > 60
                ? example.Description[..60]
                : example.Description,
            ["vsa_code_hint"] = example.VsaCode,
            ["severity"] = EstimateSeverity(example.VsaCode),
            ["position_clock"] = FormatClock(example.ClockPosition),
            ["extent_percent"] = (object?)null,
            ["height_mm"] = (object?)null,
            ["width_mm"] = (object?)null,
            ["intrusion_percent"] = (object?)null,
            ["cross_section_reduction_percent"] = (object?)null,
            ["diameter_reduction_mm"] = (object?)null,
            ["notes"] = (object?)null
        };

        var response = new Dictionary<string, object?>
        {
            ["meter"] = example.MeterPosition,
            ["time_in_video"] = (object?)null,
            ["pipe_material"] = GuessMaterial(example.Material),
            ["pipe_diameter_mm"] = (object?)null,
            ["findings"] = new[] { finding },
            ["image_quality"] = "gut",
            ["is_empty_frame"] = false
        };

        return JsonSerializer.Serialize(response);
    }

    /// <summary>Schaetzt Schweregrad anhand VSA-Code Praefix.</summary>
    private static int EstimateSeverity(string code)
    {
        if (code.Length < 2) return 2;
        return code[..2].ToUpperInvariant() switch
        {
            "BA" => code.Length >= 3 && code[2] == 'C' ? 4 : 3, // Bruch=4, sonst Riss=3
            "BB" => 2,  // Betrieblich
            "BC" => 2,  // Anschluss
            "BD" => 1,  // Allgemein
            _ => 2
        };
    }

    /// <summary>Konvertiert Material-String in Schema-Enum.</summary>
    private static string GuessMaterial(string? material)
    {
        if (string.IsNullOrEmpty(material)) return "unbekannt";
        var m = material.ToLowerInvariant();
        if (m.Contains("beton")) return "beton";
        if (m.Contains("steinzeug")) return "steinzeug";
        if (m.Contains("pvc") || m.Contains("kunststoff") || m.Contains("polypropylen") || m.Contains("pe"))
            return "pvc";
        if (m.Contains("gfk")) return "gfk";
        if (m.Contains("stahl")) return "stahl";
        if (m.Contains("faser")) return "faserzement";
        return "unbekannt";
    }

    /// <summary>Formatiert Uhrzeitlage fuer JSON-Antwort.</summary>
    private static string? FormatClock(string? clock)
    {
        if (string.IsNullOrEmpty(clock)) return null;
        // "12 Uhr" → "12:00", "6 Uhr bis 9 Uhr" → "6:00"
        var match = System.Text.RegularExpressions.Regex.Match(clock, @"(\d{1,2})");
        return match.Success ? $"{int.Parse(match.Groups[1].Value)}:00" : null;
    }

    /// <summary>
    /// Prompt fuer PDF-Bildbericht-Fotos: kein OSD erwartet, Fokus auf Schadenserkennung.
    /// </summary>
    private string BuildPdfPhotoPrompt()
    {
        return $"""
Du analysierst ein Foto aus einem Kanalinspektion-Protokoll (Bildbericht aus PDF).
Das Foto stammt aus einer TV-Inspektion eines Abwasserkanals.
WICHTIG: Es gibt KEIN OSD (On-Screen Display) in diesem Bild. Setze meter=null.

AUFGABEN:
1. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
2. Gib für jeden Schaden den wahrscheinlichsten VSA/EN 13508-2 Code als vsa_code_hint an.
3. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel/oben, "6:00" = Sohle/unten).
4. Erkenne das ROHRMATERIAL falls sichtbar.
5. Beurteile die Bildqualität.
6. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Ausdehnung (%), Querschnittsverringerung (%).

{DamageClassesPrompt}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme

TYPISCHE SCHADENSBILDER die du in Kanalfotos siehst:
- Rohranfang (BCD): Blick vom Schacht ins Rohr, Schachtwand sichtbar
- Rohrende (BCE): Blick auf Endschacht, Licht am Ende
- Seitlicher Anschluss (BCA): Runde/ovale Öffnung in der Rohrwand
- Riss (BAB): Linienförmige Unterbrechung der Rohrwand
- Wurzeleinwuchs (BBB): Wurzeln die in das Rohr hineinwachsen
- Ablagerung (BBC): Material auf der Rohrsohle
- Versatz (BAH): Verschiebung an einer Rohrverbindung
- Korrosion/Inkrustation (BBA): Verfärbungen, Ablagerungen an Rohrwand

Antworte AUSSCHLIESSLICH mit gültigem JSON gemäß Schema.
Falls kein Schaden erkennbar: findings=[], is_empty_frame=true.
Wenn du einen Schaden siehst, gib IMMER mindestens einen Finding-Eintrag zurück.
""";
    }

    private string BuildPrompt()
    {
        return $"""
Du analysierst einen Frame aus einem Kanalinspektion-Video (TV-Inspektion Abwasserkanal).

AUFGABEN:
1. Lies den METERSTAND aus dem OSD (On-Screen Display) – typisch oben oder unten im Bild (z.B. "18.40 m").
2. Erkenne das ROHRMATERIAL und den DURCHMESSER falls sichtbar.
3. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
4. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel, "6:00" = Sohle).
5. Beurteile die Bildqualität.
6. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Einragungsgrad (%), Querschnittsverringerung (%), Durchmesserverringerung (mm).

{DamageClassesPrompt}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme
Antworte AUSSCHLIESSLICH mit gültigem JSON gemäß Schema.
Falls kein Schaden erkennbar: findings=[], is_empty_frame=true.
""";
    }

    private static EnhancedFrameAnalysis MapToAnalysis(EnhancedVisionDto dto)
    {
        var findings = (dto.Findings ?? Array.Empty<EnhancedFindingDto>())
            .Where(f => !string.IsNullOrWhiteSpace(f.Label))
            .Select(f => new EnhancedFinding(
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
                Notes: f.Notes?.Trim()))
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
        CancellationToken ct = default)
    {
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();

        EnhancedVisionDto dto;
        try
        {
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
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>
    /// Analysiert mit dem schnellen 8B-Modell. Wenn die Erkennung unsicher ist
    /// und ein Reference-Modell (32B) konfiguriert ist, wird eskaliert.
    /// VRAM-Sicherheit: Primary entladen, Reference laden, danach zurueck.
    /// </summary>
    public async Task<(EnhancedFrameAnalysis Result, bool Escalated)> AnalyzeWithEscalationAsync(
        string framePngBase64,
        MultiModelFrameResult? context,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        // 1. Schnelle Analyse mit 8B
        var fast = context != null
            ? await AnalyzeWithContextAsync(framePngBase64, context, pipeDiameterMm, ct).ConfigureAwait(false)
            : await AnalyzeAsync(framePngBase64, ct).ConfigureAwait(false);

        if (_referenceModel == null || !NeedsEscalation(fast))
            return (fast, false);

        // 2. Eskalation mit 32B — SemaphoreSlim verhindert parallele Modellwechsel
        //    Timeout 120s: wenn Lock nicht verfuegbar → Graceful Degradation (8B-Ergebnis)
        if (!await _escalationLock.WaitAsync(TimeSpan.FromSeconds(120), ct).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine(
                "[EnhancedVision] Eskalation uebersprungen — Lock-Timeout (andere Eskalation laeuft)");
            return (fast, false);
        }
        try
        {
            // Primary entladen um VRAM frei zu machen
            await _client.UnloadModelAsync(_model, ct).ConfigureAwait(false);
            await Task.Delay(500, ct).ConfigureAwait(false); // GPU-Treiber VRAM-Freigabe

            try
            {
                // Reference-Modell (32B) fuer Re-Analyse nutzen
                var result = context != null
                    ? await AnalyzeWithModelAsync(_referenceModel, framePngBase64, context, pipeDiameterMm, ct)
                        .ConfigureAwait(false)
                    : await AnalyzeWithModelAsync(_referenceModel, framePngBase64, ct)
                        .ConfigureAwait(false);

                Interlocked.Increment(ref _escalationCount);
                System.Diagnostics.Debug.WriteLine(
                    $"[EnhancedVision] Eskalation #{_escalationCount} zu {_referenceModel}: " +
                    $"{result.Findings.Count} Findings, Quality={result.ImageQuality}");

                return (result, true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // OOM oder anderer Fehler beim 32B-Aufruf → 8B-Ergebnis als Fallback
                System.Diagnostics.Debug.WriteLine(
                    $"[EnhancedVision] Eskalation fehlgeschlagen ({ex.GetType().Name}): {ex.Message} — verwende 8B-Ergebnis");
                return (fast, false);
            }
            finally
            {
                // IMMER: Reference entladen, Primary wieder laden
                await _client.UnloadModelAsync(_referenceModel, CancellationToken.None)
                    .ConfigureAwait(false);
                await _client.WarmupModelAsync(_model, _numCtx, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _escalationLock.Release();
        }
    }

    /// <summary>
    /// Prueft ob eine Eskalation zum Reference-Modell noetig ist.
    /// Statische Kriterien: keine VSA-Codes, hoher Schweregrad, schlechte Qualitaet.
    /// </summary>
    private static bool NeedsEscalation(EnhancedFrameAnalysis fast)
    {
        if (fast.IsEmptyFrame || !fast.HasFindings) return false;

        // Alle Findings ohne VSA-Code → 8B hat keine Zuordnung gefunden
        bool allCodesNull = fast.Findings.All(f => string.IsNullOrEmpty(f.VsaCodeHint));

        // Hoher Schweregrad → Genauigkeit kritisch
        bool highSeverity = fast.Findings.Any(f => f.Severity >= 4);

        // Schlechte Bildqualitaet mit Findings → unsichere Erkennung
        bool poorWithFindings = fast.ImageQuality == "mittel" && fast.HasFindings;

        return allCodesNull || highSeverity || poorWithFindings;
    }

    /// <summary>
    /// Analysiert mit einem spezifischen Modell (fuer Eskalation).
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        CancellationToken ct)
    {
        var messages = BuildMessages(framePngBase64);
        try
        {
            var dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] Eskalation fehlgeschlagen ({model}): {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }
    }

    /// <summary>
    /// Analysiert mit spezifischem Modell und Multi-Model-Kontext (fuer Eskalation).
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm,
        CancellationToken ct)
    {
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();
        try
        {
            var dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] Eskalation mit Kontext fehlgeschlagen ({model}): {ex.Message}");
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }
    }

    private static string BuildContextPrompt(MultiModelFrameResult ctx, int pipeDiameterMm)
    {
        var sb = new StringBuilder();
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
        double? Meter,
        double? TimeInVideo,
        string? PipeMaterial,
        int? PipeDiameterMm,
        IReadOnlyList<EnhancedFindingDto>? Findings,
        string? ImageQuality,
        bool IsEmptyFrame);

    private sealed record EnhancedFindingDto(
        string Label,
        string? VsaCodeHint,
        int Severity,
        string? PositionClock,
        int? ExtentPercent,
        int? HeightMm,
        int? WidthMm,
        int? IntrusionPercent,
        int? CrossSectionReductionPercent,
        int? DiameterReductionMm,
        string? Notes);
}

// ── Analysis result types ─────────────────────────────────────────────────────

public sealed record EnhancedFrameAnalysis(
    double? Meter,
    string PipeMaterial,
    int? PipeDiameterMm,
    IReadOnlyList<EnhancedFinding> Findings,
    string ImageQuality,
    bool IsEmptyFrame,
    string? Error)
{
    public bool HasFindings => Findings.Count > 0;

    public static EnhancedFrameAnalysis Empty(string? error = null) =>
        new(null, "unbekannt", null,
            Array.Empty<EnhancedFinding>(), "schlecht", true, error);
}

public sealed record EnhancedFinding(
    string Label,
    string? VsaCodeHint,
    int Severity,         // 1-5
    string? PositionClock,
    int? ExtentPercent,
    int? HeightMm,
    int? WidthMm,
    int? IntrusionPercent,
    int? CrossSectionReductionPercent,
    int? DiameterReductionMm,
    string? Notes,
    // BBox normiert (0.0–1.0) — aus DINO/SAM Pipeline
    double? BboxX1Norm = null,
    double? BboxY1Norm = null,
    double? BboxX2Norm = null,
    double? BboxY2Norm = null,
    double? CentroidXNorm = null,
    double? CentroidYNorm = null
);
