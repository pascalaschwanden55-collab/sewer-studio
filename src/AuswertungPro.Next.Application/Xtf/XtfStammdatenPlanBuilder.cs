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
/// Das Ergebnis der Stammdaten-Planung.
///
/// <see cref="Hinweise"/> nennt Handaenderungen, fuer die es in der XTF kein Ziel gibt.
/// Sie sind bewusst keine Warnung: Sie halten den Export nicht auf, duerfen aber auch
/// nicht still verschwinden — sonst glaubt der Mensch, seine Aenderung sei mitgegangen.
/// </summary>
public sealed record XtfStammdatenPlan(
    IReadOnlyList<XtfRevisionPosition> Positionen,
    IReadOnlyList<string> Hinweise);

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
    ///
    /// Alle drei haengen an der SIA405-Klasse "Kanal"; <c>BaulicherZustand</c> ist von der
    /// Oberklasse "Abwasserbauwerk" geerbt.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Felder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Nutzungsart_Ist"] = FieldKeys.UsageType,
            ["Standortname"] = FieldKeys.Street,
            ["BaulicherZustand"] = FieldKeys.ConditionClass
        };

    /// <summary>
    /// Bringt einen Projektwert in die Schreibweise des XTF-Modells.
    ///
    /// Nur <c>BaulicherZustand</c> braucht das: Das Projekt fuehrt die Zustandsklasse als
    /// blosse Ziffer, SIA405 verlangt <c>Z0</c> bis <c>Z4</c>. Beide zaehlen gleich herum —
    /// 0 ist der schlechteste Zustand, 4 bedeutet keine Maengel (VSA "Erhaltung von
    /// Kanalisationen"). Es wird deshalb nur die Schreibweise angepasst, nichts umgerechnet.
    ///
    /// Alles, was nicht eindeutig in den Wertebereich passt — leer, "n/a", eine berechnete
    /// Note mit Nachkommastellen — liefert <c>null</c> und wird nicht geschrieben.
    /// </summary>
    public static string? NachXtfWert(string xtfName, string projektWert)
    {
        var wert = (projektWert ?? "").Trim();
        if (wert.Length == 0)
            return null;

        if (!string.Equals(xtfName, "BaulicherZustand", StringComparison.Ordinal))
            return wert;

        // Ein bereits fertiges "Z2" wird uebernommen, eine nackte Ziffer ergaenzt.
        if (wert.Length == 2 && (wert[0] == 'Z' || wert[0] == 'z') && wert[1] is >= '0' and <= '4')
            return "Z" + wert[1];

        return wert.Length == 1 && wert[0] is >= '0' and <= '4' ? "Z" + wert : null;
    }

    public static XtfStammdatenPlan Build(
        IEnumerable<HaltungRecord> haltungen,
        IReadOnlyList<XtfStammdatenElement> elemente)
    {
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(elemente);

        var positionen = new List<XtfRevisionPosition>();
        var hinweise = new List<string>();
        if (elemente.Count == 0)
            return new XtfStammdatenPlan(positionen, hinweise);

        var jeBezeichnung = new Dictionary<string, XtfStammdatenElement>(StringComparer.OrdinalIgnoreCase);
        // Zweiter Index ueber die Gegenrichtung: Dieselbe Haltung heisst im Projekt
        // schon mal "A-B" und in der XTF "B-A". Der Kanal ist derselbe.
        var jeGegenrichtung = new Dictionary<string, XtfStammdatenElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elemente)
        {
            var name = (element.Bezeichnung ?? "").Trim();
            // Doppelte Bezeichnungen sind nicht eindeutig zuordenbar und bleiben aussen vor.
            if (name.Length == 0)
                continue;
            if (!jeBezeichnung.TryAdd(name, element))
                jeBezeichnung[name] = null!;

            var gedreht = Gegenrichtung(name);
            if (gedreht is not null && !jeGegenrichtung.TryAdd(gedreht, element))
                jeGegenrichtung[gedreht] = null!;
        }

        foreach (var record in haltungen)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0)
                continue;

            // Die Gegenrichtung greift nur, wenn es keinen direkten Treffer gibt.
            if (!jeBezeichnung.TryGetValue(name, out var element) || element is null)
                jeGegenrichtung.TryGetValue(name, out element);

            if (element is null)
            {
                if (HatHandaenderung(record))
                    hinweise.Add($"{name}: in der XTF nicht gefunden — die Handaenderung bleibt aussen vor.");
                continue;
            }

            var felder = new List<XtfRevisionFeld>();
            foreach (var (xtfName, projektFeld) in Felder)
            {
                if (!record.FieldMeta.TryGetValue(projektFeld, out var meta) || !meta.UserEdited)
                    continue;

                var neu = NachXtfWert(xtfName, record.GetFieldValue(projektFeld) ?? "");
                if (string.IsNullOrEmpty(neu))
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

        return new XtfStammdatenPlan(positionen, hinweise);
    }

    /// <summary>
    /// Dreht "A-B" zu "B-A". Nur bei genau einem Bindestrich — alles andere ist nicht
    /// eindeutig und wird nicht geraten.
    /// </summary>
    private static string? Gegenrichtung(string name)
    {
        var teile = name.Split('-');
        if (teile.Length != 2 || teile[0].Length == 0 || teile[1].Length == 0)
            return null;

        return $"{teile[1]}-{teile[0]}";
    }

    /// <summary>True, wenn der Mensch mindestens eines der uebertragbaren Felder gesetzt hat.</summary>
    private static bool HatHandaenderung(HaltungRecord record)
    {
        foreach (var projektFeld in Felder.Values)
        {
            if (record.FieldMeta.TryGetValue(projektFeld, out var meta)
                && meta.UserEdited
                && !string.IsNullOrWhiteSpace(record.GetFieldValue(projektFeld)))
            {
                return true;
            }
        }

        return false;
    }
}
