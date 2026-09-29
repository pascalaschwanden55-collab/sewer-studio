using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// <see cref="ReadinessBrushKey"/> ist der Theme-Token-Name der Bereitschaftsfarbe;
/// <see cref="ReadinessFallbackColor"/> gilt nur, wenn keine <c>Application</c> laeuft (Unit-Test)
/// oder der Token fehlt. Die eigentliche Aufloesung (<c>TryFindResource</c>) passiert bewusst NICHT
/// hier: <see cref="Build"/> wird nach einem <c>await ... .ConfigureAwait(false)</c> auf einem
/// Threadpool-Thread aufgerufen (siehe <see cref="TrainingKnowledgeBaseStatusRefreshWorkflow"/>),
/// und WPF-Ressourcen duerfen nur vom UI-Thread gelesen werden. Die Aufloesung erfolgt deshalb erst
/// in <see cref="TrainingKnowledgeBasePresentationController.ApplyStatus"/>, das ueber
/// <c>OnUi(...)</c> auf dem UI-Thread laeuft.
/// </summary>
public sealed record TrainingKnowledgeBaseStatusPresentation(
    int SampleCount,
    int ErrorCount,
    int NewCount,
    int EmbeddingCount,
    int CodesCovered,
    string LastUpdateText,
    string ReadinessLabel,
    string ReadinessBrushKey,
    Color ReadinessFallbackColor,
    string TopCodesText);

public static class TrainingKnowledgeBaseStatusPresentationBuilder
{
    public static TrainingKnowledgeBaseStatusPresentation Build(KnowledgeBaseStatusReport status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var (readinessLabel, readinessBrushKey, readinessFallback) = status.SampleCount switch
        {
            >= 100 => ("KI-Modell einsatzbereit", "SuccessBrush", Color.FromRgb(0x4A, 0xDE, 0x80)),
            >= 25 => ("Lernbasis grundlegend", "WarningBrush", Color.FromRgb(0xFA, 0xCC, 0x15)),
            > 0 => ("Lernbasis unzureichend", "DangerBrush", Color.FromRgb(0xF8, 0x71, 0x71)),
            _ => ("Keine Trainingsdaten", "MutedBrush", Color.FromRgb(0x94, 0xA3, 0xB8))
        };

        return new TrainingKnowledgeBaseStatusPresentation(
            status.SampleCount,
            status.ErrorCount,
            status.NewCount,
            status.EmbeddingCount,
            status.CodesCovered,
            status.LatestVersionAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "—",
            readinessLabel,
            readinessBrushKey,
            readinessFallback,
            string.Join("\n", status.TopCodes.Select(c => $"{c.VsaCode}: {c.Count} Samples")));
    }
}
