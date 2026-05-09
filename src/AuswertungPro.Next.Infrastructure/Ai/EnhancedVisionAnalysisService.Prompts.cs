using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.Infrastructure.Ai;

// EnhancedVisionAnalysisService Prompt-Building: PDF-Photo-Prompt, Standard-
// Prompt fuer Vision-Frames, Context-Prompt mit DINO/SAM-Voranalyse plus
// FewShot-Helper (Severity-Estimate, Material-Guess, Clock-Format).
// Aus dem Hauptdatei extrahiert (Slice 15b).
public sealed partial class EnhancedVisionAnalysisService
{
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

{ActiveDamageClassesPrompt}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme

WICHTIG: Das label-Feld ist IMMER ein VSA-Code, z.B.:
- Rohranfang (Blick vom Schacht ins Rohr, grosse runde Oeffnung) → label="BCD" (NICHT "BACB"!)
- Rohrende → label="BCE"
- Seitlicher Anschluss (runde Oeffnung in der Wand) → label="BCAAA" (NICHT "BACB"!)
- Rohrverbindung (Fuge zwischen Segmenten) → label="BAJC" (NICHT "BAIA"!)
- Riss laengs → label="BABBA"
- Bruch/Loch (fehlende Wandung, gezackte Kanten) → label="BACB"
- Wurzeleinwuchs → label="BBA"
- Ablagerung hart → label="BBCC"
- Infiltration (Wasser dringt aktiv durch Wand) → label="BBFA" (NICHT bei Restwasser am Rohranfang!)
- Inkrustation → label="BBB"
- Bogen nach links → label="BCCAY"

Antworte AUSSCHLIESSLICH auf Deutsch mit gueltigem JSON gemaess Schema.
Falls kein Schaden UND kein Steuercode erkennbar: findings=[], is_empty_frame=true.

PFLICHT-MELDUNGEN (IMMER melden wenn sichtbar, auch ohne Schaden!):
- Rohroeffnung von vorne sichtbar (rundes Loch, Rohr dahinter) → label="BCD", severity=1
- Rohrende erreicht (Schacht sichtbar am Ende) → label="BCE", severity=1
- Richtungsaenderung/Bogen im Rohr → label="BCC", severity=1
- Seitliche Oeffnung in der Rohrwand → label="BCA", severity=1
Diese BC-Codes sind KEINE Schaeden, muessen aber IMMER gemeldet werden!
""";
    }

    private string BuildPrompt()
    {
        return $"""
Du analysierst einen Frame aus einem Kanalinspektion-Video (TV-Inspektion Abwasserkanal).

AUFGABEN (in dieser Reihenfolge!):
1. Bestimme den AUFNAHMETYP (view_type): axial, nahaufnahme, schwenk oder schacht.
   Bei nahaufnahme oder schwenk: findings=[], is_empty_frame=true (NICHT codieren!).
2. Lies den METERSTAND aus dem OSD (On-Screen Display) – typisch oben oder unten im Bild (z.B. "18.40 m").
3. Erkenne das ROHRMATERIAL und den DURCHMESSER falls sichtbar.
4. Erkenne ALLE sichtbaren Schäden/Anomalien mit Schweregrad 1-5 (1=kaum, 5=sehr schwer).
5. Gib für jeden Schaden die Uhrzeitlage an (z.B. "12:00" = Scheitel, "6:00" = Sohle).
6. Beurteile die Bildqualität.
7. Schätze, wenn erkennbar, Schadensmaße: Höhe (mm), Breite (mm), Einragungsgrad (%), Querschnittsverringerung (%), Durchmesserverringerung (mm).

{ActiveDamageClassesPrompt}

SCHWEREGRAD-SKALA (entspricht VSA Zustandsklasse):
1 = Optische Auffälligkeit, kein Handlungsbedarf
2 = Leichter Schaden, Beobachtung empfohlen
3 = Mittlerer Schaden, Sanierung mittelfristig
4 = Schwerer Schaden, Sanierung kurzfristig
5 = Kritischer Schaden, Sofortmassnahme
WICHTIG: Das label-Feld ist IMMER ein VSA-Code (z.B. "BABBA", "BCAAA", "BBFA"). KEIN Freitext.
Antworte AUSSCHLIESSLICH auf Deutsch mit gueltigem JSON gemaess Schema.

KRITISCH — PFLICHT-MELDUNGEN (IMMER melden, severity=1):
- Runde Rohroeffnung sichtbar → label="BCD" (Rohranfang)
- Rohrende/Schacht am Ende → label="BCE" (Rohrende)
- Richtungsaenderung/Kurve → label="BCC" (Bogen)
- Seitliche Oeffnung in Rohrwand → label="BCA" (Anschluss)
Diese sind KEINE Schaeden, muessen aber IMMER als Finding gemeldet werden!
Nur wenn WIRKLICH nichts sichtbar ist: findings=[], is_empty_frame=true.
WICHTIG: Setze view_type IMMER korrekt — bei Nahaufnahme/Schwenk werden Findings verworfen.
""";
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
            sb.AppendLine("ERKANNTE OBJEKTE (Florence-2):");
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
}
