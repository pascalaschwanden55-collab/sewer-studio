using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Exportiert Training-Samples als lokales YOLO-Dataset.
/// </summary>
public sealed class YoloDatasetExportService
{
    public async Task<YoloExportResult> ExportAsync(
        IReadOnlyList<TrainingSample> samples,
        string outputDir,
        double trainSplit = 0.8,
        Action<int, string>? progress = null,
        CancellationToken ct = default)
    {
        var approved = samples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && TrainingSampleEligibility.Evaluate(s).IsEligible
                        && !string.IsNullOrEmpty(s.FramePath)
                        && File.Exists(s.FramePath))
            .ToList();

        if (approved.Count == 0)
            return new YoloExportResult(false, "Keine Approved-Samples mit Frames gefunden.", 0, 0, 0, null);

        var classNames = approved
            .Select(s => NormalizeClassName(s.Code))
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var classMap = new Dictionary<string, int>();
        for (var i = 0; i < classNames.Count; i++)
            classMap[classNames[i]] = i;

        var trainImgDir = Path.Combine(outputDir, "images", "train");
        var valImgDir = Path.Combine(outputDir, "images", "val");
        var trainLblDir = Path.Combine(outputDir, "labels", "train");
        var valLblDir = Path.Combine(outputDir, "labels", "val");
        Directory.CreateDirectory(trainImgDir);
        Directory.CreateDirectory(valImgDir);
        Directory.CreateDirectory(trainLblDir);
        Directory.CreateDirectory(valLblDir);

        var rng = new Random(42);
        var shuffled = approved.OrderBy(_ => rng.Next()).ToList();
        var trainCount = (int)(shuffled.Count * trainSplit);

        var trainExported = 0;
        var valExported = 0;
        var skipped = 0;

        for (var i = 0; i < shuffled.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sample = shuffled[i];

            var isTrain = i < trainCount;
            var imgDir = isTrain ? trainImgDir : valImgDir;
            var lblDir = isTrain ? trainLblDir : valLblDir;

            var className = NormalizeClassName(sample.Code);
            if (!classMap.TryGetValue(className, out var classId))
            {
                skipped++;
                continue;
            }

            var ext = Path.GetExtension(sample.FramePath);
            var baseName = $"{sample.SampleId}_{className}";
            var imgDest = Path.Combine(imgDir, baseName + ext);
            var lblDest = Path.Combine(lblDir, baseName + ".txt");

            try
            {
                File.Copy(sample.FramePath, imgDest, overwrite: true);
            }
            catch
            {
                skipped++;
                continue;
            }

            var (xc, yc, w, h) = GetBoundingBox(sample);
            var labelLine = string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1:F6} {2:F6} {3:F6} {4:F6}",
                classId,
                xc,
                yc,
                w,
                h);
            await File.WriteAllTextAsync(lblDest, labelLine + "\n", ct).ConfigureAwait(false);

            if (isTrain) trainExported++;
            else valExported++;

            var total = trainExported + valExported;
            if (total % 10 == 0)
                progress?.Invoke((int)(100.0 * i / shuffled.Count), $"{total}/{shuffled.Count} exportiert");
        }

        var yamlPath = Path.Combine(outputDir, "data.yaml");
        var yamlLines = new List<string>
        {
            $"# SewerStudio YOLO Dataset - {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"# {trainExported + valExported} Samples ({trainExported} train, {valExported} val), {classNames.Count} Klassen",
            $"path: {outputDir}",
            "train: images/train",
            "val: images/val",
            "",
            $"nc: {classNames.Count}",
            $"names: [{string.Join(", ", classNames.Select(c => $"'{c}'"))}]"
        };
        await File.WriteAllLinesAsync(yamlPath, yamlLines, ct).ConfigureAwait(false);

        var totalExported = trainExported + valExported;
        progress?.Invoke(100, $"Fertig: {totalExported} Samples, {classNames.Count} Klassen");

        return new YoloExportResult(
            true,
            null,
            totalExported,
            trainExported,
            valExported,
            yamlPath);
    }

    private static (double xCenter, double yCenter, double width, double height) GetBoundingBox(TrainingSample sample)
    {
        if (sample.HasBbox)
        {
            return (
                sample.BboxXCenter!.Value,
                sample.BboxYCenter!.Value,
                sample.BboxWidth!.Value,
                sample.BboxHeight!.Value);
        }

        return (0.5, 0.5, 0.8, 0.8);
    }

    private static string NormalizeClassName(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;

        var dotIdx = code.IndexOf('.');
        if (dotIdx > 0) code = code[..dotIdx];

        return code.Length >= 3 ? code[..3].ToUpperInvariant() : code.ToUpperInvariant();
    }
}

public sealed record YoloExportResult(
    bool IsSuccess,
    string? Error,
    int TotalExported,
    int TrainCount,
    int ValCount,
    string? YamlPath);
