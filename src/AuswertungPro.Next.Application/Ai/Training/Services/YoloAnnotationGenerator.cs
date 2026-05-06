using System.Globalization;
using System.IO;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Training.Services;

namespace AuswertungPro.Next.Application.Ai.Training.Services;

/// <summary>
/// Konvertiert GroundTruthEntry + Frame-Paare in YOLO-Format-Labels
/// und exportiert ein komplettes YOLO-Dataset (images/labels, train/val Split).
/// </summary>
public static class YoloAnnotationGenerator
{
    /// <summary>
    /// Erzeugt eine YOLO-Label-Zeile fuer einen GroundTruthEntry.
    /// Full-Frame-Bbox (0.5 0.5 1.0 1.0), da PDF-Protokolle keine Pixel-Annotationen haben.
    /// Gibt null zurueck fuer Steuercodes, unbekannte oder leere Codes.
    /// </summary>
    public static string? GenerateLabelLine(GroundTruthEntry entry)
    {
        var defect = YoloDefectTaxonomy.FromVsaCode(entry.VsaCode);
        if (defect is null)
            return null;

        return $"{defect.Value.ClassId} 0.5 0.5 1.0 1.0";
    }

    /// <summary>
    /// Exportiert ein komplettes YOLO-Dataset mit Train/Val-Split.
    /// Erstellt images/train, images/val, labels/train, labels/val und data.yaml.
    /// </summary>
    public static async Task<DatasetStats> ExportDatasetAsync(
        IReadOnlyList<(GroundTruthEntry Entry, string FramePath)> mappings,
        string outputDir,
        double trainSplit = 0.8,
        CancellationToken ct = default)
    {
        // Verzeichnisstruktur anlegen
        var imgTrain = Path.Combine(outputDir, "images", "train");
        var imgVal = Path.Combine(outputDir, "images", "val");
        var lblTrain = Path.Combine(outputDir, "labels", "train");
        var lblVal = Path.Combine(outputDir, "labels", "val");

        Directory.CreateDirectory(imgTrain);
        Directory.CreateDirectory(imgVal);
        Directory.CreateDirectory(lblTrain);
        Directory.CreateDirectory(lblVal);

        // Gueltige Eintraege filtern
        var valid = new List<(int Index, string Label, string FramePath)>();
        int skipped = 0;

        for (int i = 0; i < mappings.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (entry, framePath) = mappings[i];
            var label = GenerateLabelLine(entry);

            if (label is null || !File.Exists(framePath))
            {
                skipped++;
                continue;
            }

            valid.Add((i, label, framePath));
        }

        // Deterministischer Train/Val-Split (Seed 42)
        var rng = new Random(42);
        var shuffled = valid.OrderBy(_ => rng.Next()).ToList();
        int trainCount = (int)(shuffled.Count * trainSplit);

        var classCounts = new Dictionary<int, int>();

        for (int i = 0; i < shuffled.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (origIdx, label, framePath) = shuffled[i];
            bool isTrain = i < trainCount;

            var imgDir = isTrain ? imgTrain : imgVal;
            var lblDir = isTrain ? lblTrain : lblVal;

            // Dateiname: Index-basiert um Eindeutigkeit zu garantieren
            var ext = Path.GetExtension(framePath);
            var baseName = $"frame_{origIdx:D6}";

            // Bild kopieren
            var destImg = Path.Combine(imgDir, baseName + ext);
            File.Copy(framePath, destImg, overwrite: true);

            // Label schreiben
            var destLbl = Path.Combine(lblDir, baseName + ".txt");
            await File.WriteAllTextAsync(destLbl, label, ct);

            // Klassenstatistik
            var classId = int.Parse(label.Split(' ')[0], CultureInfo.InvariantCulture);
            classCounts[classId] = classCounts.GetValueOrDefault(classId) + 1;
        }

        // data.yaml schreiben
        var yaml = YoloDefectTaxonomy.GenerateDataYaml(outputDir);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "data.yaml"), yaml, ct);

        return new DatasetStats
        {
            Total = mappings.Count,
            Train = trainCount,
            Val = shuffled.Count - trainCount,
            Skipped = skipped,
            ClassCounts = classCounts
        };
    }

    /// <summary>
    /// Statistik ueber das exportierte Dataset.
    /// </summary>
    public sealed class DatasetStats
    {
        public int Total { get; init; }
        public int Train { get; init; }
        public int Val { get; init; }
        public int Skipped { get; init; }
        public Dictionary<int, int> ClassCounts { get; init; } = new();
    }
}
