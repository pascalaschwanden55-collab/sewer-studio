using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Exportiert Training-Samples als lokales YOLO-Dataset (ohne Sidecar).
/// Erzeugt die Standard YOLO-Ordnerstruktur:
///   dataset/
///     images/train/  + images/val/
///     labels/train/  + labels/val/
///     data.yaml
///
/// Label-Format pro Zeile: class_id x_center y_center width height (normiert 0-1)
/// </summary>
public sealed class YoloDatasetExportService
{
    /// <summary>
    /// Exportiert Approved-Samples als YOLO-Dataset.
    /// </summary>
    /// <param name="samples">Alle Training-Samples (nur Approved werden exportiert).</param>
    /// <param name="outputDir">Zielverzeichnis fuer das Dataset.</param>
    /// <param name="trainSplit">Anteil Train vs. Validation (0.8 = 80% Train).</param>
    /// <param name="progress">Fortschritts-Callback (0-100%).</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task<YoloExportResult> ExportAsync(
        IReadOnlyList<TrainingSample> samples,
        string outputDir,
        double trainSplit = 0.8,
        bool stratifiedByClass = false,
        bool requireRealBboxes = false,
        Action<int, string>? progress = null,
        CancellationToken ct = default)
    {
        // Nur Approved-Samples mit Frame-Bild
        var approved = samples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && !string.IsNullOrEmpty(s.FramePath)
                        && File.Exists(s.FramePath)
                        && (!requireRealBboxes || s.HasBbox))
            .ToList();

        if (approved.Count == 0)
            return new YoloExportResult(false, "Keine Approved-Samples mit Frames gefunden.", 0, 0, 0, null);

        // Klassen-Mapping: VSA-Hauptcode (3 Zeichen) → class_id
        var classNames = approved
            .Select(s => NormalizeClassName(s.Code))
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var classMap = new Dictionary<string, int>();
        for (int i = 0; i < classNames.Count; i++)
            classMap[classNames[i]] = i;

        // Ordnerstruktur erstellen
        var trainImgDir = Path.Combine(outputDir, "images", "train");
        var valImgDir = Path.Combine(outputDir, "images", "val");
        var trainLblDir = Path.Combine(outputDir, "labels", "train");
        var valLblDir = Path.Combine(outputDir, "labels", "val");
        Directory.CreateDirectory(trainImgDir);
        Directory.CreateDirectory(valImgDir);
        Directory.CreateDirectory(trainLblDir);
        Directory.CreateDirectory(valLblDir);

        // Shuffle und Split (optional: stratifiziert nach Klasse)
        var rng = new Random(42); // Deterministisch fuer Reproduzierbarkeit
        var assignments = BuildAssignments(approved, trainSplit, stratifiedByClass, rng);

        int trainExported = 0;
        int valExported = 0;
        int skipped = 0;
        int fallbackBoxes = 0;
        int realBoxes = 0;

        for (int i = 0; i < assignments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (sample, isTrain) = assignments[i];

            var imgDir = isTrain ? trainImgDir : valImgDir;
            var lblDir = isTrain ? trainLblDir : valLblDir;

            // Klasse bestimmen
            string className = NormalizeClassName(sample.Code);
            if (!classMap.TryGetValue(className, out int classId))
            {
                skipped++;
                continue;
            }

            // Bild kopieren
            string ext = Path.GetExtension(sample.FramePath);
            string baseName = $"{sample.SampleId}_{className}";
            string imgDest = Path.Combine(imgDir, baseName + ext);
            string lblDest = Path.Combine(lblDir, baseName + ".txt");

            try
            {
                File.Copy(sample.FramePath, imgDest, overwrite: true);
            }
            catch
            {
                skipped++;
                continue;
            }

            // Label schreiben (YOLO-Format: class_id x_center y_center width height)
            var (xc, yc, w, h) = GetBoundingBox(sample);
            if (sample.HasBbox) realBoxes++;
            else fallbackBoxes++;
            var labelLine = string.Format(CultureInfo.InvariantCulture,
                "{0} {1:F6} {2:F6} {3:F6} {4:F6}", classId, xc, yc, w, h);
            await File.WriteAllTextAsync(lblDest, labelLine + "\n", ct);

            if (isTrain) trainExported++; else valExported++;
            int total = trainExported + valExported;
            if (total % 10 == 0)
                progress?.Invoke((int)(100.0 * i / assignments.Count), $"{total}/{assignments.Count} exportiert");
        }

        // data.yaml schreiben
        var yamlPath = Path.Combine(outputDir, "data.yaml");
        var yamlLines = new List<string>
        {
            $"# SewerStudio YOLO Dataset — {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"# {trainExported + valExported} Samples ({trainExported} train, {valExported} val), {classNames.Count} Klassen",
            $"path: {outputDir}",
            "train: images/train",
            "val: images/val",
            "",
            $"nc: {classNames.Count}",
            $"names: [{string.Join(", ", classNames.Select(c => $"'{c}'"))}]"
        };
        await File.WriteAllLinesAsync(yamlPath, yamlLines, ct);

        int totalExported = trainExported + valExported;
        progress?.Invoke(100, $"Fertig: {totalExported} Samples, {classNames.Count} Klassen");

        return new YoloExportResult(
            true, null,
            totalExported,
            trainExported,
            valExported,
            yamlPath,
            realBoxes,
            fallbackBoxes);
    }

    private static List<(TrainingSample Sample, bool IsTrain)> BuildAssignments(
        IReadOnlyList<TrainingSample> approved,
        double trainSplit,
        bool stratifiedByClass,
        Random rng)
    {
        if (!stratifiedByClass)
        {
            var shuffled = approved.OrderBy(_ => rng.Next()).ToList();
            int trainCount = (int)(shuffled.Count * trainSplit);
            var output = new List<(TrainingSample Sample, bool IsTrain)>(shuffled.Count);
            for (int i = 0; i < shuffled.Count; i++)
                output.Add((shuffled[i], i < trainCount));
            return output;
        }

        var grouped = approved
            .GroupBy(s => NormalizeClassName(s.Code))
            .ToList();

        var assignments = new List<(TrainingSample Sample, bool IsTrain)>(approved.Count);
        foreach (var group in grouped)
        {
            var groupShuffled = group.OrderBy(_ => rng.Next()).ToList();
            var groupCount = groupShuffled.Count;
            if (groupCount == 0) continue;

            int groupTrainCount;
            if (groupCount == 1)
            {
                groupTrainCount = 1;
            }
            else
            {
                groupTrainCount = (int)Math.Round(groupCount * trainSplit, MidpointRounding.AwayFromZero);
                groupTrainCount = Math.Max(1, Math.Min(groupCount - 1, groupTrainCount));
            }

            for (int i = 0; i < groupShuffled.Count; i++)
                assignments.Add((groupShuffled[i], i < groupTrainCount));
        }

        // Nochmals mischen fuer bessere Durchmischung der Export-Reihenfolge
        return assignments.OrderBy(_ => rng.Next()).ToList();
    }

    /// <summary>
    /// Bestimmt die BoundingBox fuer ein Sample.
    /// Wenn das Sample aus einem Eingabemarker stammt (mit echten BBox-Koordinaten),
    /// werden diese verwendet. Sonst Fallback auf zentrierte Box.
    /// </summary>
    private static (double xCenter, double yCenter, double width, double height) GetBoundingBox(TrainingSample sample)
    {
        // Zusaetzliche Frame-Daten pruefen (BBox aus OverlayGeometry)
        // Die BBox wird bei Eingabemarker-Events im OverlayGeometry.Points gespeichert:
        // Points[0] = TopLeft (normiert), Points[1] = BottomRight (normiert)
        // Diese werden ueber das CodingEvent in den Sample-Notes oder AdditionalFramePaths kodiert.

        // Echte BBox aus Eingabemarker/Overlay (wenn vorhanden)
        if (sample.HasBbox)
            return (sample.BboxXCenter!.Value, sample.BboxYCenter!.Value,
                    sample.BboxWidth!.Value, sample.BboxHeight!.Value);

        // Fallback: Zentrierte Box (fuer aeltere Samples ohne BBox)
        return (0.5, 0.5, 0.8, 0.8);
    }

    /// <summary>
    /// Normalisiert den VSA-Code auf den Hauptcode (3 Zeichen).
    /// BCAEB → BCA, BABBA → BAB, BCD → BCD
    /// </summary>
    private static string NormalizeClassName(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        // Punkt-Notation entfernen: BAB.B → BAB
        var dotIdx = code.IndexOf('.');
        if (dotIdx > 0) code = code[..dotIdx];
        // Auf 3 Zeichen kuerzen (Hauptcode)
        return code.Length >= 3 ? code[..3].ToUpperInvariant() : code.ToUpperInvariant();
    }
}

/// <summary>Ergebnis des YOLO-Exports.</summary>
public sealed record YoloExportResult(
    bool IsSuccess,
    string? Error,
    int TotalExported,
    int TrainCount,
    int ValCount,
    string? YamlPath,
    int RealBboxCount = 0,
    int FallbackBboxCount = 0
);
