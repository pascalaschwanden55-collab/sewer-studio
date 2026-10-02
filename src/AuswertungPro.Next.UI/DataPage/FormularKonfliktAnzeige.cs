using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Konflikthinweis (W01) fuer ein Formular, das ihn selbst zeigt — das Detailfenster
/// (<see cref="RecordDetailsWindow"/>) von Haltungen und Schaechten. Solange es angemeldet ist,
/// geht der Hinweis in dieses Fenster und nicht an die Seite (Liste oder Schublade dahinter):
/// Der Benutzer sieht ihn dort, wo er eingegeben hat (Review PR #75). Der Wortlaut ist derselbe
/// (<see cref="DataPageKonfliktHinweis"/>), mit der Beschriftung des Formularfelds.
/// </summary>
public static class FormularKonfliktAnzeige
{
    /// <summary>Meldet <paramref name="zeige"/> an allen Feldern an; Dispose meldet wieder ab.</summary>
    public static IDisposable Verbinde(IEnumerable<RecordDetailGroup> gruppen, Action<string> zeige)
    {
        ArgumentNullException.ThrowIfNull(gruppen);
        ArgumentNullException.ThrowIfNull(zeige);

        var felder = gruppen.SelectMany(g => g.Items).ToList();
        Action<RecordDetailItem, string, string> zuhoerer = (item, aktuell, eingabe)
            => zeige(DataPageKonfliktHinweis.MitBeschriftung(item.Label, aktuell, eingabe));
        foreach (var feld in felder)
            feld.KonfliktGemeldet += zuhoerer;

        return new Abmeldung(() =>
        {
            foreach (var feld in felder)
                feld.KonfliktGemeldet -= zuhoerer;
        });
    }

    private sealed class Abmeldung(Action abmelden) : IDisposable
    {
        private Action? _abmelden = abmelden;

        public void Dispose()
        {
            _abmelden?.Invoke();
            _abmelden = null;
        }
    }
}
