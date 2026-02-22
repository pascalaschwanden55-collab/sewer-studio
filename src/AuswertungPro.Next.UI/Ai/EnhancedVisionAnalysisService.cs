using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Verbesserte Vision-Analyse mit vollständiger Schadensklassenliste nach
/// DIN EN 13508-2 und VSA-DSS (analog zu Pallons 300+ Klassen).
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
ERKENNBARE SCHADENSKLASSEN (DIN EN 13508-2 / VSA-DSS):

STRUKTURELLE SCHÄDEN:
- Riss (längs, quer, diagonal, ringförmig, verzweigt)
- Bruch (partiell, total, Einsturz)
- Scherben/Splitternde Wandung
- Deformation (vertikal, horizontal)
- Wanddurchdringung
- Loch in der Wandung
- Fehlstelle/offene Muffenverbindung
- Versatz (vertikal, horizontal)
- Einragung Stutzen/Anschluss

OBERFLÄCHENSCHÄDEN:
- Korrosion (gleichmäßig, ungleichmäßig)
- Ausbrüche/Abplatzungen
- Porosität/Rillen
- Bewuchs (Pilze, Algen, Wurzeln)
- Inkrustation (Kalkablagerung, Fettablagerung)

BETRIEBLICHE STÖRUNGEN:
- Ablagerung (verfestigt, nicht verfestigt)
- Fremdkörper (Steine, Textilien, sonstige)
- Fettablagerung
- Hindernisse

ANSCHLÜSSE / VERBINDUNGEN:
- Falschanschluss (Einleitung oberhalb Sohlhöhe)
- Undichter Anschluss
- Offener/fehlender Anschluss
- Eindringendes Wasser (Infiltration)

SONSTIGES:
- Wasserstand (ruhend, fließend)
- Schaumblasen (Gasbildung)
""";

    private readonly OllamaClient _client;
    private readonly string _model;

    public EnhancedVisionAnalysisService(OllamaClient client, string model)
    {
        _client = client;
        _model = model;
    }

    public async Task<EnhancedFrameAnalysis> AnalyzeAsync(
        string framePngBase64,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt();

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
        catch (Exception ex)
        {
            return EnhancedFrameAnalysis.Empty(ex.Message);
        }

        return MapToAnalysis(dto);
    }

    private static string BuildPrompt() => $"""
Du analysierst einen Frame aus einem Kanalinspektion-Video (TV-Inspektion Abwasserkanal).

AUFGABEN:
1. Lies den METERSTAND aus dem OSD (On-Screen Display) – typisch oben oder unten im Bild (z.B. "18.40 m").
2. Erkenne das ROHRMATERIAL und den DURCHMESSER falls sichtbar.
3. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
4. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel, "6:00" = Sohle).
5. Beurteile die Bildqualität.

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
    string? Notes
);
