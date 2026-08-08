namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>
/// Vorabdurchlauf eines Videos: liefert die Liste verdaechtiger Stellen zum
/// menschlichen Bestaetigen oder Korrigieren.
///
/// Bewusst ein Vorablauf und keine Live-Einblendung im Player: Bei conf 0,50 ist
/// jeder zweite Vorschlag falsch, und eine falsche Box waehrend des Codierens
/// kostet Vertrauen bei jedem zweiten Auftreten. Eine Liste prueft der Mensch
/// gebuendelt, und ihre Trefferquote laesst sich gegen die Protokolle nachmessen.
/// </summary>
public interface IBendSuggestionScanService
{
    Task<BendSuggestionScanResult> ScanAsync(
        BendSuggestionScanRequest request,
        CancellationToken cancellationToken);
}
