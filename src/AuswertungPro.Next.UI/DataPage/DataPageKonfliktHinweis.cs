using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Der eine Wortlaut fuer den Konflikt-Hinweis der Haltungs-Eingabefelder (Nachpruefung W01):
/// Der Datensatz hat sich seit der Anzeige geaendert, die neuere Korrektur bleibt, und die
/// verworfene Eingabe wird sichtbar gemeldet.
///
/// Eingabefelder-Schublade und Aufklapp-Liste lesen denselben Text. Zwei Kopien wuerden bei der
/// naechsten Korrektur auseinanderlaufen, und der Benutzer saehe je nach Ansicht etwas anderes.
/// </summary>
public static class DataPageKonfliktHinweis
{
    public static string Text(string fieldName, string aktuellerWert, string eingabe)
    {
        var label = FieldCatalog.Get(fieldName).Label;
        return $"„{label}“ wurde inzwischen auf „{aktuellerWert}“ geändert. Die Eingabe „{eingabe}“ wurde nicht übernommen.";
    }
}
