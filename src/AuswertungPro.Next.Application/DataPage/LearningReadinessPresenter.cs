namespace AuswertungPro.Next.Application.DataPage;

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
}
