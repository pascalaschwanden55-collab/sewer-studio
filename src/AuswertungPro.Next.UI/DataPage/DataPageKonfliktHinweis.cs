using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Der eine Wortlaut fuer den Konflikt-Hinweis der Eingabefelder (Nachpruefung W01) — fuer
/// Haltungen (Katalogfeld) und Schaechte (Spaltenname, <see cref="Schacht"/>):
/// Der Datensatz hat sich seit der Anzeige geaendert, die neuere Korrektur bleibt, und die
/// verworfene Eingabe wird sichtbar gemeldet.
///
/// Eingabefelder-Schublade und Aufklapp-Liste lesen denselben Text. Zwei Kopien wuerden bei der
/// naechsten Korrektur auseinanderlaufen, und der Benutzer saehe je nach Ansicht etwas anderes.
/// </summary>
public static class DataPageKonfliktHinweis
{
    public static string Text(string fieldName, string aktuellerWert, string eingabe)
        => MitBeschriftung(FieldCatalog.Get(fieldName).Label, aktuellerWert, eingabe);

    /// <summary>Schachtfeld: Die Beschriftung kommt aus der Spaltenregel der Schachtseite.</summary>
    public static string Schacht(string feld, string aktuellerWert, string eingabe)
        => MitBeschriftung(SchaechteColumnPolicy.GetDisplayHeader(feld), aktuellerWert, eingabe);

    private static string MitBeschriftung(string label, string aktuellerWert, string eingabe)
        => $"„{label}“ wurde inzwischen auf „{aktuellerWert}“ geändert. Die Eingabe „{eingabe}“ wurde nicht übernommen.";
}
