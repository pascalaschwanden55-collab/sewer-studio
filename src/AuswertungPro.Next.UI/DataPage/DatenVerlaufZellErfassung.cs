using System.Windows.Threading;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Optik Aufgabe 16: haelt den Erfassungsbereich einer Tabellenzelle vom Oeffnen bis nach dem Commit.
/// Beginnen muss er beim OEFFNEN: Die Auswahl der Zustandsklasse schreibt schon vor dem Schliessen
/// ins Feld. Geschlossen wird er erst, wenn der Commit der Zelle durch ist (die Bindung schreibt nach
/// <c>CellEditEnding</c>) — mit Eingabe-Prioritaet, also vor der naechsten Taste (Strg+Z).
/// Ein neuer Beginn schliesst einen noch offenen oder erst vorgemerkten Bereich sofort; sonst wuerde
/// die naechste Zelle Teil des alten Schritts (der Verlauf haengt einen inneren Bereich an den aeusseren).
/// </summary>
internal sealed class DatenVerlaufZellErfassung
{
    private IDisposable? _offen;
    private IDisposable? _ausstehend;

    public void Beginne(IDisposable? erfassung)
    {
        Beende();
        _offen = erfassung;
    }

    public void BeendeNachCommit(Dispatcher dispatcher)
    {
        var erfassung = _offen;
        _offen = null;
        if (erfassung is null)
            return;
        _ausstehend = erfassung;
        dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (ReferenceEquals(_ausstehend, erfassung))
                _ausstehend = null;
            erfassung.Dispose();
        }));
    }

    /// <summary>Schliesst sofort; das Schliessen eines Bereichs ist mehrfach sicher.</summary>
    public void Beende()
    {
        var ausstehend = _ausstehend;
        var offen = _offen;
        _ausstehend = null;
        _offen = null;
        ausstehend?.Dispose();
        offen?.Dispose();
    }
}
