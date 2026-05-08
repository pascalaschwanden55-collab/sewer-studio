using System;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Exportiert Training-Samples als lokales YOLO-Dataset (ohne Sidecar).
/// Erzeugt die Standard YOLO-Ordnerstruktur:
///   dataset/
///     images/train/  + images/val/
///     labels/train/  + labels/val/
///     data.yaml
///
/// Label-Format pro Zeile (Detection): class_id x_center y_center width height
/// Label-Format pro Zeile (Segment, Slice 1): class_id x1 y1 x2 y2 ... xn yn
/// (alle Werte normiert 0..1).
///
/// Tech-Debt (Slice 2): <see cref="ExportAsync"/> baut die Class-Map aktuell
/// SELBST aus den Sample-Codes (lokale Reihenfolge). <see cref="AppendSampleAsync"/>
/// nutzt dagegen <c>VsaYoloClassMap.TryGetClassId</c>. Das kann zu unter-
/// schiedlichen Class-IDs zwischen Append-Pfad (Operateur-Annotation) und
/// Export-Pfad (TrainingCenter Re-Export) fuehren. Slice 2 muss die beiden
/// Pfade harmonisieren (gemeinsame Class-Map ueber VsaYoloClassMap).
/// </summary>
public sealed class YoloDatasetExportService : IYoloDatasetWriter
{
    private readonly string? _datasetRoot;

    public YoloDatasetExportService() : this(null) { }

    /// <summary>
    /// Konstruktor mit injizierbarem Dataset-Root fuer
    /// <see cref="AppendSampleAsync"/>. <see cref="ExportAsync"/> nimmt den
    /// Pfad weiterhin als Methoden-Parameter — Bestandskode bleibt unangetastet.
    /// </summary>
    public YoloDatasetExportService(string? datasetRoot)
    {
        _datasetRoot = datasetRoot;
    }

    /// <summary>
    /// Slice 1: schreibt einen einzelnen, vom Operateur annotierten Sample
    /// als YOLO-seg-Eintrag (Bild + Label-Polygon, normiert).
    /// Wirft, wenn die Class-ID nicht in <see cref="VsaYoloClassMap"/> ist
    /// (kein Auto-Create) oder das Polygon-JSON degeneriert ist.
    /// </summary>
    public async Task<string> AppendSampleAsync(
        TrainingSample sample,
        MaskPreview preview,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_datasetRoot is null)
            throw new InvalidOperationException(
                "Dataset-Root nicht gesetzt — fuer AppendSampleAsync den Konstruktor mit datasetRoot nutzen.");

        if (string.IsNullOrWhiteSpace(sample.SampleId))
            throw new ArgumentException("sample.SampleId ist Pflicht.", nameof(sample));
        if (string.IsNullOrWhiteSpace(sample.FramePath))
            throw new ArgumentException("sample.FramePath ist Pflicht.", nameof(sample));
        if (preview.MaskWidth <= 0 || preview.MaskHeight <= 0)
            throw new ArgumentException("MaskPreview.MaskWidth/Height muessen > 0 sein.", nameof(preview));

        if (!VsaYoloClassMap.TryGetClassId(sample.Code, out var classId))
            throw new InvalidOperationException(
                $"VSA-Code '{sample.Code}' ist nicht in VsaYoloClassMap — Auto-Create im Append-Pfad ist deaktiviert (Slice 1 stabile Class-IDs).");

        var polygon = ParsePolygonJson(preview.PolygonJson);
        if (polygon.Count < 3)
            throw new InvalidOperationException(
                $"Polygon hat nur {polygon.Count} Punkte — YOLO-seg braucht mindestens 3.");

        // Slice 1 schreibt ausschliesslich in den Train-Split. Der Split-
        // Algorithmus aus ExportAsync ist hier bewusst nicht aktiv; eine
        // spaetere Slice 2 kann hier eine deterministische Validation-Quote
        // ergaenzen.
        var trainImgDir = Path.Combine(_datasetRoot, "images", "train");
        var trainLblDir = Path.Combine(_datasetRoot, "labels", "train");
        Directory.CreateDirectory(trainImgDir);
        Directory.CreateDirectory(trainLblDir);

        var className = NormalizeClassName(sample.Code);
        var ext = Path.GetExtension(sample.FramePath);
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        var baseName = $"{sample.SampleId}_{className}";
        var imageDest = Path.Combine(trainImgDir, baseName + ext);
        var labelDest = Path.Combine(trainLblDir, baseName + ".txt");

        // Frame kopieren — File.Copy wirft IOException, wenn der Source-Frame
        // weg ist; das wollen wir hochbubblen, damit CommitAsync den Sample
        // nicht als YoloWritten=true markiert.
        File.Copy(sample.FramePath, imageDest, overwrite: true);

        var labelLine = BuildSegLabelLine(classId, polygon, preview.MaskWidth, preview.MaskHeight);
        await File.WriteAllTextAsync(labelDest, labelLine + "\n", ct).ConfigureAwait(false);

        return labelDest;
    }

    private static List<(double X, double Y)> ParsePolygonJson(string polygonJson)
    {
        if (string.IsNullOrWhiteSpace(polygonJson))
            throw new InvalidOperationException("MaskPreview.PolygonJson ist leer.");

        // Erwartetes Format: [[x1, y1], [x2, y2], ...]
        try
        {
            using var doc = JsonDocument.Parse(polygonJson);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("PolygonJson ist kein Array.");

            var points = new List<(double X, double Y)>(arr.GetArrayLength());
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 2)
                    throw new InvalidOperationException("PolygonJson-Punkt muss [x, y] sein.");
                var x = item[0].GetDouble();
                var y = item[1].GetDouble();
                points.Add((x, y));
            }
            return points;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("MaskPreview.PolygonJson ist nicht parsebar.", ex);
        }
    }

    private static string BuildSegLabelLine(int classId, List<(double X, double Y)> polygonPx, int width, int height)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(classId.ToString(CultureInfo.InvariantCulture));
        foreach (var (x, y) in polygonPx)
        {
            var nx = Math.Clamp(x / width, 0.0, 1.0);
            var ny = Math.Clamp(y / height, 0.0, 1.0);
            sb.Append(' ');
            sb.Append(nx.ToString("F6", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(ny.ToString("F6", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

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
