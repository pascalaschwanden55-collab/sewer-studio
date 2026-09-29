using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration): zeichnet jeden Aufruf als
/// Textzeile auf ("fortschritt:0,5", "unbestimmt", "fehler", "beenden"), damit Tests die
/// Reihenfolge und die genauen Werte pruefen koennen, ohne ein echtes Fenster zu brauchen.
/// </summary>
internal sealed class FakeTaskbarFortschritt : ITaskbarFortschritt
{
    public FakeTaskbarFortschritt()
    {
    }

    /// <summary>Ereignisse in eine von aussen mitgegebene, GEMEINSAME Liste schreiben - so laesst
    /// sich die Reihenfolge gegen andere Fakes (z. B. einen Dialog-Fake) beweisen, nicht nur
    /// die Taskleisten-Aufrufe fuer sich allein.</summary>
    public FakeTaskbarFortschritt(List<string> geteilteAufrufe) => Aufrufe = geteilteAufrufe;

    public List<string> Aufrufe { get; } = new();

    public void SetzeFortschritt(double anteil)
        => Aufrufe.Add($"fortschritt:{anteil.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");

    public void SetzeUnbestimmt() => Aufrufe.Add("unbestimmt");

    public void Fehler() => Aufrufe.Add("fehler");

    public void Beenden() => Aufrufe.Add("beenden");
}
