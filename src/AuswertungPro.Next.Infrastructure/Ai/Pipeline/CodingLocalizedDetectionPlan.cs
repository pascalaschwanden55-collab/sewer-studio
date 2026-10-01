using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Bindet qualifizierte Detektorboxen an SAM, ohne sie als DINO auszugeben.</summary>
/// <param name="Detections">Gültige Boxen nach Zusammenführung gleicher Hauptgruppe und Stelle.</param>
/// <param name="RejectedYoloBoxes">Wegen ungültiger Quelle, Werte, Bildgrenzen oder nicht belegter Klasse verworfene YOLO-Boxen. Erfolgreich zusammengeführte Boxen zählen nicht.</param>
/// <param name="RejectedDinoBoxes">Wegen ungültiger Werte verworfene DINO-Boxen. Erfolgreich zusammengeführte Boxen zählen nicht.</param>
public sealed record CodingLocalizedDetectionPlan(
    IReadOnlyList<CodingLocalizedDetection> Detections, int RejectedYoloBoxes, int RejectedDinoBoxes = 0)
{
    /// <summary>Anzahl fachlich oder technisch verworfener Eingabeboxen, ohne erfolgreiche Zusammenführungen.</summary>
    public int RejectedBoxes => RejectedYoloBoxes + RejectedDinoBoxes;
    public static CodingLocalizedDetectionPlan Build(
        IReadOnlyList<DinoDetectionDto> dino, YoloResponse? yolo,
        string? qualifiedArtifactSha256, int width, int height)
        => BuildCore(dino, yolo?.Detections,
            yolo?.DetectorQualified == true && CodingLocalizedDetection.IsSha256(qualifiedArtifactSha256)
                && string.Equals(qualifiedArtifactSha256, yolo.DetectorArtifactSha256, StringComparison.OrdinalIgnoreCase),
            yolo?.DetectorArtifactSha256, yolo?.ModelName, width, height, false);

    public static CodingLocalizedDetectionPlan BuildCandidate(
        IReadOnlyList<DinoDetectionDto> dino, CodingDetectorCandidateFrame candidate,
        ReadOnlySpan<byte> image, int width, int height)
    {
        var error = candidate.ValidateForImage(image);
        if (error is not null) throw new ArgumentException(error, nameof(candidate));
        return BuildCore(dino, candidate.Detections, true, candidate.ActualWeightSha256,
            candidate.CandidateId, width, height, true);
    }

    private static CodingLocalizedDetectionPlan BuildCore(
        IReadOnlyList<DinoDetectionDto> dino, IReadOnlyList<YoloDetectionDto>? yolo,
        bool sourceBound, string? artifactSha256, string? modelName,
        int width, int height, bool developmentCandidate)
    {
        var dinoCandidates = dino.Select(d => new CodingLocalizedDetection(
            CodingDetectionSource.Dino, d.Label, d.X1, d.Y1, d.X2, d.Y2,
            DinoConfidence: d.Confidence, RequiresReview: developmentCandidate,
            DevelopmentCandidate: developmentCandidate)).ToArray();
        var detections = dinoCandidates.Where(d => d.HasValidEvidence).ToList();
        var rejectedDino = dinoCandidates.Length - detections.Count;
        if (yolo is null) return new(detections, 0, rejectedDino);
        var rejected = 0;
        foreach (var box in yolo)
        {
            var code = ResolveYoloMainCode(box.ClassName);
            var origin = new CodingLocalizedDetection(CodingDetectionSource.Yolo,
                box.ClassName, box.X1, box.Y1, box.X2, box.Y2, box.Confidence,
                YoloArtifactSha256: artifactSha256, VsaMainCode: code, RequiresReview: developmentCandidate,
                YoloModelName: modelName, DevelopmentCandidate: developmentCandidate);
            if (!sourceBound || code is null || !origin.HasValidEvidence || width <= 0 || height <= 0
                || box.X2 > width || box.Y2 > height)
            {
                rejected++;
                continue;
            }

            // Nur gleiche Hauptgruppe und gleiche Stelle vereinigen. Andere Gruppen
            // bleiben eigene Befunde; daraus wird kein Modellkonsens erfunden.
            var same = detections.FindIndex(d => SameCode(d, code) && Overlap(d, origin) >= 0.5);
            if (same >= 0)
            {
                var previous = detections[same];
                if (previous.HasYolo)
                {
                    if (previous.YoloConfidence >= origin.YoloConfidence) continue;
                    origin = origin with { DinoConfidence = previous.DinoConfidence };
                }
                else origin = origin with { DinoConfidence = previous.DinoConfidence };
                if (origin.DinoConfidence.HasValue) origin = origin with { Source = CodingDetectionSource.YoloAndDino };
                detections[same] = origin;
            }
            else detections.Add(origin);
        }
        return new(detections, rejected, rejectedDino);
    }

    public static string? ResolveYoloMainCode(string? label)
    {
        // Nur die eingefrorenen 15 Klassen; SONST und generisches Bodenmaterial
        // belegen keinen speicherbaren VSA-Code. Keinen Sammel-Untercode erfinden.
        if (label is null || !YoloDetectClassMapV3.Classes.ContainsKey(label)
            || label.Equals("SONST_schaden", StringComparison.OrdinalIgnoreCase)
            || label.Equals("BBD_boden", StringComparison.OrdinalIgnoreCase)) return null;
        return label.Split('_')[0].ToUpperInvariant();
    }

    public static CodingLocalizedDetection? Match(
        SamMaskResult mask, IReadOnlyList<CodingLocalizedDetection> sources, int width, int height)
    {
        if (mask.Bbox.Count < 4 || width <= 0 || height <= 0) return null;
        var box = mask.Bbox;
        if (box.Any(v => !double.IsFinite(v)) || box[0] < 0 || box[1] < 0
            || box[2] > width || box[3] > height || box[2] <= box[0] || box[3] <= box[1]) return null;
        // Der vorhandene SAM-Vertrag gibt die geclampte Eingabebox zurueck
        // (sam_wrapper.py), nicht den engeren Umriss der Maske. Nur eine genaue,
        // eindeutige Zuordnung verhindert fremde Confidence bei verschachtelten Boxen.
        var matches = sources.Where(d => d.HasValidEvidence
                && (!d.HasYolo || d.X2 <= width && d.Y2 <= height)
                && (!d.HasYolo || ResolveYoloMainCode(d.Label) == d.VsaMainCode)
                && string.Equals(d.Label, mask.Label, StringComparison.OrdinalIgnoreCase)
                && Math.Abs(Math.Clamp(d.X1, 0, width) - box[0]) <= 0.000001
                && Math.Abs(Math.Clamp(d.Y1, 0, height) - box[1]) <= 0.000001
                && Math.Abs(Math.Clamp(d.X2, 0, width) - box[2]) <= 0.000001
                && Math.Abs(Math.Clamp(d.Y2, 0, height) - box[3]) <= 0.000001)
            .Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public static string? ResolveEventCode(CodingLocalizedDetection origin) =>
        origin.HasValidEvidence && origin.HasYolo
        && ResolveYoloMainCode(origin.Label) == origin.VsaMainCode
        && VsaCodeResolver.IsExactSelectableCode(origin.VsaMainCode)
            ? origin.VsaMainCode : null;

    private static bool SameCode(CodingLocalizedDetection detection, string code) =>
        string.Equals(detection.VsaMainCode ?? VsaCodeResolver.InferCodeFromLabel(detection.Label), code, StringComparison.OrdinalIgnoreCase);
    private static double Overlap(CodingLocalizedDetection a, CodingLocalizedDetection b)
    {
        var intersection = Intersection(a.X1, a.Y1, a.X2, a.Y2, b.X1, b.Y1, b.X2, b.Y2);
        var union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - intersection;
        return union > 0 ? intersection / union : 0;
    }
    private static double Intersection(double ax1, double ay1, double ax2, double ay2, double bx1, double by1, double bx2, double by2) =>
        Math.Max(0, Math.Min(ax2, bx2) - Math.Max(ax1, bx1)) * Math.Max(0, Math.Min(ay2, by2) - Math.Max(ay1, by1));
}
