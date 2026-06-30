using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingBatchImportSampleLogBuilder
{
    public static IReadOnlyList<string> Build(IReadOnlyCollection<TrainingSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var lines = new List<string>
        {
            $"  -> {samples.Count} Samples (Status: Neu, Freigabe ueber Review):"
        };
        lines.AddRange(samples.Select(s =>
            $"     {s.Code} @ {s.MeterStart:F2}m [{s.Status}] - {s.Beschreibung}"));
        return lines;
    }
}
