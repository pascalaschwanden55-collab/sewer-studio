using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Der eine Rueckschreibweg mit Konfliktschutz fuer Eingabeformulare (Nachpruefung W01) —
/// fuer Haltungen (<see cref="DataPageDetailItemFactory"/>) und Schaechte
/// (<see cref="SchaechteRecordDetailsBuilder"/>). Vorher hatte nur die Haltungsseite die Regel;
/// das Schachtformular schrieb eine Eingabe still ueber eine inzwischen geaenderte
/// Tabellenkorrektur (Deepscan 02.10.2026).
///
/// Regel: Das Formular merkt sich im <see cref="RecordDetailItem.Ausgangswert"/> den
/// Datensatzwert, auf dem die Anzeige beruht. Traegt der Datensatz beim Uebernehmen inzwischen
/// einen anderen Wert (Tabelle, Rueckgaengig, Dienst), bleibt diese neuere Korrektur stehen, das
/// Formular zeigt sie, und die verworfene Eingabe wird gemeldet. Sonst wird geschrieben und der
/// echte Datensatzwert danach als neuer Ausgangswert uebernommen.
/// </summary>
public static class FormularKonfliktschutz
{
    /// <summary>
    /// Der Datensatz traegt inzwischen einen anderen Wert als den, auf dem die Eingabe beruht,
    /// und die Eingabe ist nicht zufaellig genau dieser Wert.
    /// </summary>
    public static bool IstKonflikt(string? ausgangswert, string? aktuellerWert, string? eingabe)
        => !string.Equals(aktuellerWert ?? string.Empty, ausgangswert ?? string.Empty, StringComparison.Ordinal)
           && !string.Equals(aktuellerWert ?? string.Empty, eingabe ?? string.Empty, StringComparison.Ordinal);

    /// <param name="item">Das Formularfeld; null nur waehrend des Aufbaus.</param>
    /// <param name="datensatzwert">Liest den aktuellen Wert des Feldes aus dem Datensatz.</param>
    /// <param name="eingabe">Die Formulareingabe.</param>
    /// <param name="schreiben">Schreibt die Eingabe in den Datensatz (Weg der Seite).</param>
    /// <param name="konflikt">Meldet (aktueller Datensatzwert, verworfene Eingabe).</param>
    public static void Rueckschreiben(
        RecordDetailItem? item,
        Func<string> datensatzwert,
        string eingabe,
        Action schreiben,
        Action<string, string>? konflikt)
    {
        ArgumentNullException.ThrowIfNull(datensatzwert);
        ArgumentNullException.ThrowIfNull(schreiben);

        var aktuell = datensatzwert() ?? string.Empty;
        if (item is not null && IstKonflikt(item.Ausgangswert, aktuell, eingabe))
        {
            item.UebernehmeAusDatensatz(aktuell);
            konflikt?.Invoke(aktuell, eingabe);
            return;
        }

        schreiben();
        // Nach dem Schreiben den echten Datensatzwert uebernehmen (Umbenennung kann abweichen).
        item?.UebernehmeAusDatensatz(datensatzwert() ?? string.Empty);
    }
}
