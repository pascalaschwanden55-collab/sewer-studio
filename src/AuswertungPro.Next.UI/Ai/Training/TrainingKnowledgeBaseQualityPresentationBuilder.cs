using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseQualityPresentation(
    string CoverageGapsText,
    int CoverageGapsCount,
    string AccuracyText,
    int StaleSampleCount,
    string TrendText,
    string TrendDirection,
    IReadOnlyList<string> LogLines);

public static class TrainingKnowledgeBaseQualityPresentationBuilder
{
    public static TrainingKnowledgeBaseQualityPresentation Build(
        KnowledgeBaseQualityReport quality,
        IReadOnlyList<SelfTrainingRunSnapshot> runs)
    {
        ArgumentNullException.ThrowIfNull(quality);
        ArgumentNullException.ThrowIfNull(runs);

        var last5 = runs.TakeLast(5).ToList();
        var trendText = last5.Count > 0
            ? string.Join("\n", last5.Select(r =>
                $"{r.TimestampUtc.ToLocalTime():dd.MM. HH:mm} \u2014 " +
                $"Exact: {r.ExactPercent:P0} | Partial: {r.PartialPercent:P0} | " +
                $"Miss: {r.MismatchPercent:P0} | Leer: {r.NoFindingsPercent:P0}"))
            : "Noch keine Selbsttraining-Laeufe";

        var direction = "";
        if (last5.Count >= 2)
        {
            var delta = last5[^1].ExactPercent - last5[^2].ExactPercent;
            const double trendThreshold = 0.02;
            const double tolerance = 0.000000001;
            direction = delta > trendThreshold + tolerance
                ? "\u2191"
                : delta < -trendThreshold - tolerance ? "\u2193" : "\u2192";
        }

        var logLines = new List<string>();
        if (quality.StaleSampleCount > 0)
            logLines.Add($"KB-Qualitaet: {quality.StaleSampleCount} veraltete Samples erkannt (manuell pruefen im Tab 'Samples')");

        return new TrainingKnowledgeBaseQualityPresentation(
            quality.CoverageGapsText,
            quality.CoverageGapsCount,
            quality.AccuracyText,
            quality.StaleSampleCount,
            trendText,
            direction,
            logLines);
    }
}
