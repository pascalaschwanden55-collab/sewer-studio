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
    /// Die Nutzungsart als fachliches Konzept — und wie die beiden Modellfassungen es
    /// jeweils schreiben. Nur das Regenwasser unterscheidet sich: SIA405 2015 kennt
    /// <c>Regenabwasser</c>, SIA405 2020 stattdessen <c>Niederschlagsabwasser</c>. Keine
    /// der beiden Fassungen kennt den Wert der anderen — eine feste Liste wuerde also je
    /// nach Datei das Falsche schreiben.
    /// </summary>
    private static readonly (string[] Projekt, string Bis2015, string Ab2020)[] Nutzungsarten =
    [
        (["schmutzwasser", "schmutzabwasser"], "Schmutzabwasser", "Schmutzabwasser"),
        (["regenwasser", "regenabwasser", "niederschlagsabwasser"], "Regenabwasser", "Niederschlagsabwasser"),
        (["mischwasser", "mischabwasser"], "Mischabwasser", "Mischabwasser"),
        (["entlastetes mischabwasser", "entlastetes_mischabwasser"],
            "entlastetes_Mischabwasser", "entlastetes_Mischabwasser"),
        (["reinwasser", "reinabwasser"], "Reinabwasser", "Reinabwasser"),
        (["bachwasser"], "Bachwasser", "Bachwasser"),
        (["industrieabwasser", "industriewasser"], "Industrieabwasser", "Industrieabwasser"),
        (["andere"], "andere", "andere"),
        (["unbekannt"], "unbekannt", "unbekannt")
    ];

    /// <summary>
    /// Bringt einen Projektwert in die Schreibweise des XTF-Modells.
    ///
    /// <c>BaulicherZustand</c>: Das Projekt fuehrt die Zustandsklasse als blosse Ziffer,
    /// SIA405 verlangt <c>Z0</c> bis <c>Z4</c>. Beide zaehlen gleich herum — 0 ist der
    /// schlechteste Zustand, 4 bedeutet keine Maengel (VSA "Erhaltung von Kanalisationen").
    /// Es wird deshalb nur die Schreibweise angepasst, nichts umgerechnet.
    ///
    /// <c>Nutzungsart_Ist</c>: Das Projekt fuehrt "Schmutzwasser", das Modell verlangt
    /// "Schmutzabwasser" — der Import benennt beim Lesen ausdruecklich um
    /// (<c>XtfValueNormalizer</c>), also muss der Rueckweg dasselbe tun. Fuer das
    /// Regenwasser entscheidet die Modellfassung der Datei.
    ///
    /// Alles, was nicht eindeutig in den Wertebereich passt — "n/a", eine berechnete Note
    /// mit Nachkommastellen, ein unbekannter Begriff — liefert <c>null</c> und wird nicht
    /// geschrieben. Ein erfundener Wert waere schlimmer als eine fehlende Angabe.
    /// </summary>
    public static string? NachXtfWert(string xtfName, string projektWert, string? modell = null)
    {
        var wert = (projektWert ?? "").Trim();
        if (wert.Length == 0)
            return null;

        if (string.Equals(xtfName, "BaulicherZustand", StringComparison.Ordinal))
        {
            // Ein bereits fertiges "Z2" wird uebernommen, eine nackte Ziffer ergaenzt.
            if (wert.Length == 2 && (wert[0] == 'Z' || wert[0] == 'z') && wert[1] is >= '0' and <= '4')
                return "Z" + wert[1];

            return wert.Length == 1 && wert[0] is >= '0' and <= '4' ? "Z" + wert : null;
        }

        if (!string.Equals(xtfName, "Nutzungsart_Ist", StringComparison.Ordinal))
            return wert;

        foreach (var (projekt, bis2015, ab2020) in Nutzungsarten)
        {
            if (!projekt.Contains(wert.ToLowerInvariant()))
                continue;

            if (string.Equals(bis2015, ab2020, StringComparison.Ordinal))
                return bis2015;

            // Nur hier entscheidet die Modellfassung. Ist sie unbekannt, wird nichts
            // geschrieben — lieber eine Luecke als der Wert der falschen Fassung.
            return IstModell2020OderNeuer(modell) switch
            {
                true => ab2020,
                false => bis2015,
                _ => null
            };
        }

        return null;
    }

    /// <summary>
    /// Liest die Fassung aus dem Modellnamen der Datei, etwa "SIA405_ABWASSER_2015_LV95".
    /// <c>null</c> bedeutet: nicht erkennbar.
    /// </summary>
    private static bool? IstModell2020OderNeuer(string? modell)
    {
        var treffer = System.Text.RegularExpressions.Regex.Match(modell ?? "", @"(19|20)\d{2}");
        return treffer.Success && int.TryParse(treffer.Value, out var jahr)
            ? jahr >= 2020
            : null;
    }

    /// <param name="modell">
    /// Der Modellname aus dem Kopf der XTF, etwa "SIA405_ABWASSER_2015_LV95". Er entscheidet
    /// bei der Nutzungsart ueber die richtige Schreibweise.
    /// </param>
    public static XtfStammdatenPlan Build(
        IEnumerable<HaltungRecord> haltungen,
        IReadOnlyList<XtfStammdatenElement> elemente,
        string? modell = null)
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

                var roh = (record.GetFieldValue(projektFeld) ?? "").Trim();
                var neu = NachXtfWert(xtfName, roh, modell);
                if (string.IsNullOrEmpty(neu))
                {
                    // Ein gesetzter, aber nicht abbildbarer Wert darf nicht still
                    // verschwinden — und erst recht nicht geraten werden.
                    if (roh.Length > 0)
                    {
                        hinweise.Add(
                            $"{name}: {xtfName} = \"{roh}\" passt in dieser XTF zu keinem " +
                            "gueltigen Wert — nicht geschrieben.");
                    }

                    continue;
                }

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
