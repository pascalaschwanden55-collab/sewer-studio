using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Ein Stammdaten-Objekt aus der SIA405-XTF (Klasse "Kanal") mit seinen gelesenen Werten.
/// </summary>
public sealed record XtfStammdatenElement(
    string Tid,
    string Bezeichnung,
    IReadOnlyDictionary<string, string> Werte);

/// <summary>
/// Erzeugt Planpositionen fuer die Stammdaten der SIA405-XTF.
///
/// Geschrieben wird ausschliesslich, was der Mensch von Hand gesetzt hat: Nur Felder mit
/// <c>UserEdited</c> kommen in Frage. Importierte oder berechnete Werte bleiben aussen vor —
/// sonst wuerde die Revision Werte zurueckschreiben, die aus derselben Datei stammen.
///
/// Reine Rechnung ohne Dateizugriff und ohne Mutation.
/// </summary>
public static class XtfStammdatenPlanBuilder
{
    /// <summary>
    /// Abbildung XTF-Element -> Projektfeld. Bewusst kurz gehalten: Nur Felder, deren
    /// Bedeutung in beiden Modellen eindeutig dieselbe ist.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Felder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Nutzungsart_Ist"] = FieldKeys.UsageType,
            ["Standortname"] = FieldKeys.Street
        };

    public static IReadOnlyList<XtfRevisionPosition> Build(
        IEnumerable<HaltungRecord> haltungen,
        IReadOnlyList<XtfStammdatenElement> elemente)
    {
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(elemente);

        var positionen = new List<XtfRevisionPosition>();
        if (elemente.Count == 0)
            return positionen;

        var jeBezeichnung = new Dictionary<string, XtfStammdatenElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elemente)
        {
            var name = (element.Bezeichnung ?? "").Trim();
            // Doppelte Bezeichnungen sind nicht eindeutig zuordenbar und bleiben aussen vor.
            if (name.Length == 0)
                continue;
            if (!jeBezeichnung.TryAdd(name, element))
                jeBezeichnung[name] = null!;
        }

        foreach (var record in haltungen)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0
                || !jeBezeichnung.TryGetValue(name, out var element)
                || element is null)
            {
                continue;
            }

            var felder = new List<XtfRevisionFeld>();
            foreach (var (xtfName, projektFeld) in Felder)
            {
                if (!record.FieldMeta.TryGetValue(projektFeld, out var meta) || !meta.UserEdited)
                    continue;

                var neu = (record.GetFieldValue(projektFeld) ?? "").Trim();
                if (neu.Length == 0)
                    continue;

                element.Werte.TryGetValue(xtfName, out var alt);
                alt = (alt ?? "").Trim();
                if (string.Equals(alt, neu, StringComparison.Ordinal))
                    continue;

                felder.Add(new XtfRevisionFeld(xtfName, alt.Length == 0 ? null : alt, neu));
            }

            if (felder.Count == 0)
                continue;

            positionen.Add(new XtfRevisionPosition(
                XtfRevisionAenderung.Geaendert,
                element.Tid,
                UntersuchungTid: "",
                name,
                Code: "",
                Meter: null,
                felder));
        }

        return positionen;
    }
}
