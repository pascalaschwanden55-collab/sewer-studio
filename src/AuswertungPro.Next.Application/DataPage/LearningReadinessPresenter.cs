using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Application.DataPage;

public sealed record LearningReadinessPresentation(
    string Info,
    string Color,
    string Text,
    bool IsVisible);

/// <summary>
/// Reine Praesentations-Logik fuer die Lernbereitschafts-Ampel (Rot/Gelb/Gruen).
/// Berechnet Farbe und Text anhand der Anzahl gelernter Faelle.
/// Aus <c>DataPageViewModel.UpdateLearningTrafficLight</c> extrahiert (verhaltensneutral).
/// </summary>
public static class LearningReadinessPresenter
{
    /// <summary>Ab dieser Anzahl Faelle leuchtet die Ampel Gruen (starkes Modell).</summary>
    public const int StrongModelThreshold = 100;

    /// <summary>Ab dieser Anzahl Faelle leuchtet die Ampel Gelb (KI-Modell verfuegbar).</summary>
    public const int MinimumSamplesForTraining = 25;

    /// <summary>
    /// Berechnet Ampelfarbe (CSS-Hex) und Ampeltext fuer die angezeigte Fallanzahl.
    /// </summary>
    /// <param name="totalSamples">Gesamtanzahl der gelernten Faelle.</param>
    /// <returns>Tupel (Farbe, Text) – z.B. ("#2E7D32", "Gruen").</returns>
    public static (string Color, string Text) Evaluate(int totalSamples)
    {
        if (totalSamples >= StrongModelThreshold)
            return ("#2E7D32", "Gruen");

        if (totalSamples >= MinimumSamplesForTraining)
            return ("#F9A825", "Gelb");

        return ("#C62828", "Rot");
    }

    public static LearningReadinessPresentation Build(
        MeasureLearningStats stats,
        int? similarCases = null,
        decimal? estimatedCost = null)
    {
        var (color, text) = Evaluate(stats.TotalSamples);

        if (stats.TotalSamples <= 0)
            return new LearningReadinessPresentation("Lernbasis: 0 Faelle", color, text, true);

        var suffix = string.Empty;
        if (similarCases is not null && similarCases.Value > 0)
        {
            suffix = estimatedCost is null
                ? $" / letzte Schaetzung aus {similarCases.Value} aehnlichen Haltungen"
                : $" / letzte Kostenschaetzung {estimatedCost.Value:0.00} aus {similarCases.Value} aehnlichen Haltungen";
        }

        var modelText = stats.TrainedModelAvailable
            ? $" / KI-Modell aktiv ({stats.TrainedModelSamples ?? 0} Faelle)"
            : $" / KI-Modell ab {MinimumSamplesForTraining} Faellen";

        return new LearningReadinessPresentation(
            $"Lernbasis: {stats.TotalSamples} Faelle{suffix}{modelText}",
            color,
            text,
            true);
    }
}
