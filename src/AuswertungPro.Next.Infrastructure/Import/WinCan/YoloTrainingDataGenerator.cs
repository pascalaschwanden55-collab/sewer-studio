using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Generiert YOLO-Trainingskandidaten aus dem Vergleich KI vs. Operateur.
/// 3 Qualitaetsstufen: Green (direkt ins Training), Yellow (Review), Red (verworfen).
///
/// Logik:
/// - Operateur-Code liefert Klassen-Truth
/// - YOLO liefert Lokalisierungshypothese (Box)
/// - Matching-Regeln entscheiden ob daraus ein Trainingssample wird
/// </summary>
public static class YoloTrainingDataGenerator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // VSA-Code zu YOLO-Klassen-ID Mapping
    private static readonly Dictionary<string, int> VsaToYoloClass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BAA"] = 0,  ["BAB"] = 1,  ["BAC"] = 2,  ["BAF"] = 3,
        ["BAG"] = 4,  ["BAH"] = 5,  ["BAI"] = 6,  ["BAJ"] = 7,
        ["BAK"] = 8,  ["BBA"] = 9,  ["BBB"] = 10, ["BBC"] = 11,
        ["BBF"] = 12, ["BCA"] = 13, ["BCC"] = 14, ["BCD"] = 15,
        ["BCE"] = 16, ["BDA"] = 17, ["BDD"] = 18,
    };

    /// <summary>Ergebnis der Kandidaten-Generierung fuer einen Frame.</summary>
    public sealed record TrainingCandidate(
        [property: JsonPropertyName("frame_path")] string FramePath,
        [property: JsonPropertyName("quality")] string Quality,
        [property: JsonPropertyName("operator_code")] string OperatorCode,
        [property: JsonPropertyName("yolo_class_id")] int YoloClassId,
        [property: JsonPropertyName("yolo_label")] string YoloLabel,
        [property: JsonPropertyName("yolo_confidence")] double YoloConfidence,
        [property: JsonPropertyName("box_x1")] double BoxX1,
        [property: JsonPropertyName("box_y1")] double BoxY1,
        [property: JsonPropertyName("box_x2")] double BoxX2,
        [property: JsonPropertyName("box_y2")] double BoxY2,
        [property: JsonPropertyName("img_width")] int ImgWidth,
        [property: JsonPropertyName("img_height")] int ImgHeight,
        [property: JsonPropertyName("reason")] string Reason);

    /// <summary>Statistik pro Durchlauf.</summary>
    public sealed record GenerationStats(
        [property: JsonPropertyName("green")] int Green,
        [property: JsonPropertyName("yellow")] int Yellow,
        [property: JsonPropertyName("red")] int Red,
        [property: JsonPropertyName("negatives")] int Negatives,
        [property: JsonPropertyName("per_class")] Dictionary<string, int> PerClass);

    /// <summary>
    /// Klassifiziert ein YOLO-Ergebnis gegen den Operateur-Code.
    /// </summary>
    /// <param name="operatorCode">VSA-Code vom Operateur (z.B. "BCCAY").</param>
    /// <param name="yoloLabel">YOLO-Klassenname (z.B. "BCC").</param>
    /// <param name="yoloConfidence">YOLO-Confidence (0.0-1.0).</param>
    /// <param name="boxAreaNorm">Box-Flaeche normalisiert (0.0-1.0).</param>
    /// <param name="neighborFrameMatch">Gleiche Klasse in Nachbar-Frames erkannt?</param>
    /// <returns>"green", "yellow" oder "red".</returns>
    public static string ClassifyQuality(
        string operatorCode,
        string? yoloLabel,
        double yoloConfidence,
        double boxAreaNorm,
        bool neighborFrameMatch)
    {
        if (string.IsNullOrEmpty(yoloLabel))
            return "red"; // Keine YOLO-Detection

        var opMain = operatorCode.Length >= 3 ? operatorCode[..3] : operatorCode;
        var yoloMain = yoloLabel.Length >= 3 ? yoloLabel[..3] : yoloLabel;

        // Klassen-Match: Operateur und YOLO stimmen ueberein?
        bool classMatch = opMain.Equals(yoloMain, StringComparison.OrdinalIgnoreCase);

        // Konflikt-Klassen: aehnliche Codes die verwechselt werden
        bool isConflictClass = IsConflictPair(opMain, yoloMain);

        // GREEN: Hohe Sicherheit
        if (classMatch && yoloConfidence >= 0.50 && neighborFrameMatch)
            return "green";

        if (classMatch && yoloConfidence >= 0.65)
            return "green";

        // YELLOW: Mittlere Sicherheit, braucht Review
        if (classMatch && yoloConfidence >= 0.30)
            return "yellow";

        if (isConflictClass && yoloConfidence >= 0.40)
            return "yellow"; // Verwechslung moeglich, Review noetig

        // Unplausible Box (>60% Bildflaeche oder <1%)
        if (boxAreaNorm > 0.60 || boxAreaNorm < 0.01)
            return "red";

        // Kein Klassen-Match und keine Konflikt-Klasse
        if (!classMatch && !isConflictClass)
            return "red";

        return "yellow"; // Im Zweifel: Review
    }

    /// <summary>Prueft ob zwei VSA-Hauptcodes haeufig verwechselt werden.</summary>
    public static bool IsConflictPair(string code1, string code2)
    {
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BCA-BCC", "BCC-BCA",  // Anschluss vs. Bogen
            "BAI-BAJ", "BAJ-BAI",  // Dichtung vs. Rohrverbindung
            "BAB-BAC", "BAC-BAB",  // Riss vs. Bruch
            "BBC-BBB", "BBB-BBC",  // Ablagerung vs. Inkrustation
            "BCD-BCA", "BCA-BCD",  // Rohranfang vs. Anschluss
            "BAF-BAB", "BAB-BAF",  // Korrosion vs. Riss
        };
        return conflicts.Contains($"{code1}-{code2}");
    }

    /// <summary>
    /// Generiert YOLO-Label-Datei im YOLO-seg Format.
    /// </summary>
    public static string ToYoloSegLabel(int classId, double x1, double y1, double x2, double y2, int imgW, int imgH)
    {
        // YOLO-seg: class_id x1 y1 x2 y2 x3 y3 x4 y4 (normalisierte Polygon-Punkte)
        double nx1 = x1 / imgW, ny1 = y1 / imgH;
        double nx2 = x2 / imgW, ny2 = y2 / imgH;
        return $"{classId} {nx1:F6} {ny1:F6} {nx2:F6} {ny1:F6} {nx2:F6} {ny2:F6} {nx1:F6} {ny2:F6}";
    }

    /// <summary>
    /// Speichert Kandidaten sortiert nach Qualitaet in Ordner-Struktur.
    /// </summary>
    public static GenerationStats SaveCandidates(
        List<TrainingCandidate> candidates,
        string outputRoot)
    {
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int green = 0, yellow = 0, red = 0, negatives = 0;

        foreach (var c in candidates)
        {
            var qualityDir = Path.Combine(outputRoot, c.Quality);
            var imgDir = Path.Combine(qualityDir, "images");
            var lblDir = Path.Combine(qualityDir, "labels");
            Directory.CreateDirectory(imgDir);
            Directory.CreateDirectory(lblDir);

            // Frame kopieren (wenn Pfad existiert)
            var srcPath = c.FramePath;
            if (!File.Exists(srcPath)) continue;

            var fileName = Path.GetFileName(srcPath);
            var dstImg = Path.Combine(imgDir, fileName);
            var dstLbl = Path.Combine(lblDir, Path.ChangeExtension(fileName, ".txt"));

            if (!File.Exists(dstImg))
                File.Copy(srcPath, dstImg);

            // Label schreiben
            if (c.YoloClassId >= 0)
            {
                var label = ToYoloSegLabel(c.YoloClassId, c.BoxX1, c.BoxY1, c.BoxX2, c.BoxY2, c.ImgWidth, c.ImgHeight);
                File.WriteAllText(dstLbl, label + "\n");
            }
            else
            {
                // Negativ-Beispiel: leeres Label
                File.WriteAllText(dstLbl, "");
                negatives++;
            }

            switch (c.Quality)
            {
                case "green": green++; break;
                case "yellow": yellow++; break;
                case "red": red++; break;
            }

            var classKey = c.OperatorCode.Length >= 3 ? c.OperatorCode[..3] : c.OperatorCode;
            stats[classKey] = stats.TryGetValue(classKey, out var cnt) ? cnt + 1 : 1;
        }

        // Index speichern
        File.WriteAllText(
            Path.Combine(outputRoot, "_candidates.json"),
            JsonSerializer.Serialize(candidates, JsonOpts));

        var genStats = new GenerationStats(green, yellow, red, negatives, stats);
        File.WriteAllText(
            Path.Combine(outputRoot, "_stats.json"),
            JsonSerializer.Serialize(genStats, JsonOpts));

        return genStats;
    }

    /// <summary>Gibt die YOLO-Klassen-ID fuer einen VSA-Code zurueck, oder -1.</summary>
    public static int GetYoloClassId(string vsaCode)
    {
        var main = vsaCode.Length >= 3 ? vsaCode[..3] : vsaCode;
        return VsaToYoloClass.TryGetValue(main, out var id) ? id : -1;
    }
}
