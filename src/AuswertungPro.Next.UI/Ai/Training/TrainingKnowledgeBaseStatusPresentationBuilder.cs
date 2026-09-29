using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseStatusPresentation(
    int SampleCount,
    int ErrorCount,
    int NewCount,
    int EmbeddingCount,
    int CodesCovered,
    string LastUpdateText,
    string ReadinessLabel,
    SolidColorBrush ReadinessBrush,
    string TopCodesText);

public static class TrainingKnowledgeBaseStatusPresentationBuilder
{
    public static TrainingKnowledgeBaseStatusPresentation Build(KnowledgeBaseStatusReport status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var (readinessLabel, readinessBrush) = status.SampleCount switch
        {
            >= 100 => ("KI-Modell einsatzbereit", ResolveBrush("SuccessBrush", 0x4A, 0xDE, 0x80)),
            >= 25 => ("Lernbasis grundlegend", ResolveBrush("WarningBrush", 0xFA, 0xCC, 0x15)),
            > 0 => ("Lernbasis unzureichend", ResolveBrush("DangerBrush", 0xF8, 0x71, 0x71)),
            _ => ("Keine Trainingsdaten", ResolveBrush("MutedBrush", 0x94, 0xA3, 0xB8))
        };

        return new TrainingKnowledgeBaseStatusPresentation(
            status.SampleCount,
            status.ErrorCount,
            status.NewCount,
            status.EmbeddingCount,
            status.CodesCovered,
            status.LatestVersionAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "\u2014",
            readinessLabel,
            readinessBrush,
            string.Join("\n", status.TopCodes.Select(c => $"{c.VsaCode}: {c.Count} Samples")));
    }

    /// <summary>
    /// Loest einen Theme-Token als Pinsel auf, damit die Bereitschaftsfarbe im Dunkelmodus stimmt.
    /// Ohne laufende Anwendung (z. B. im Unit-Test) gilt der feste Rueckfallwert.
    /// </summary>
    private static SolidColorBrush ResolveBrush(string key, byte red, byte green, byte blue)
        => System.Windows.Application.Current?.TryFindResource(key) as SolidColorBrush ?? Rgb(red, green, blue);

    private static SolidColorBrush Rgb(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
