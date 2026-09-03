using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Erzeugt Planpositionen fuer die Schaechte der SIA405-XTF (Klasse <c>Normschacht</c>).
///
/// Dieselbe Regel wie bei den Haltungen: Geschrieben wird ausschliesslich, was der Mensch
/// von Hand gesetzt hat. Ein importierter Wert geht nie in die Datei zurueck, aus der er
/// stammt.
///
/// Das Gegenstueck beim Lesen ist <see cref="XtfNormschachtStammdaten"/>; die Feldnamen
/// sind dieselben, damit ein Schacht aus verschiedenen Quellen derselbe bleibt.
///
/// Von den vier Schachtklassen der Norm traegt nur <c>Normschacht</c> Daten. <c>Deckel</c>
/// und <c>Einstiegshilfe</c> stehen in allen gepruefeten Lieferungen leer da, und die
/// Schachttiefe hat ueberhaupt kein Zielfeld — sie waere aus <c>Deckel.Kote</c> minus
/// <c>Abwasserknoten.Sohlenkote</c> abzuleiten, und <c>Deckel.Kote</c> ist ueberall leer.
///
/// Reine Rechnung ohne Dateizugriff und ohne Mutation.
/// </summary>
public static class XtfSchachtPlanBuilder
{
    /// <summary>Das Feld, in dem die Schachtnummer steht — wie beim Import.</summary>
    public const string Nummernfeld = "Schachtnummer";

    /// <summary>
    /// Abbildung XTF-Element -> Projektfeld am <c>Normschacht</c>.
    ///
    /// <c>Dimension1</c> und <c>Dimension2</c> fehlen hier bewusst: Das Programm fuehrt
    /// beide Masse in EINEM Textfeld ("600 mm", "1100 x 900 mm"). Sie laufen deshalb
    /// ueber <see cref="Abmessungen"/> und nicht ueber diese Karte.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Felder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Funktion"] = "Funktion",
            ["Material"] = "Material",
            ["BaulicherZustand"] = FieldKeys.ConditionClass,
            ["Bemerkung"] = FieldKeys.Remarks
        };

    /// <summary>
    /// Der Wert eines Schachtfeldes, unter dem Namen gelesen, den der Datensatz wirklich
    /// fuehrt.
    ///
    /// Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage, nicht nach dem
    /// Katalog: Der Eigentuemer steht dort unter "Eigentümer" mit Umlaut, waehrend
    /// <see cref="FieldKeys.Owner"/> "Eigentuemer" lautet. Wer direkt danach greift,
    /// findet nichts — und beim Export fehlte dann jeder Schacht, weil der Eigentuemer
    /// in SIA405 Pflicht ist (real aufgefallen am 2026-09-03).
    /// </summary>
    public static string? Wert(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.GetFieldValue(SchachtFeldnamen.Feld(record, gemeint));
    }

    /// <summary>True, wenn der Mensch dieses Feld gesetzt hat — unter jeder Schreibweise.</summary>
    public static bool IstHandgesetzt(SchachtRecord record, string gemeint)
    {
        ArgumentNullException.ThrowIfNull(record);
        return SchachtFeldnamen.Schreibweisen(record, gemeint).Any(record.IsUserEdited);
    }

    /// <summary>Das Projektfeld mit beiden Massen.</summary>
    public const string Dimensionsfeld = "Dimension";

    /// <summary>
    /// Bringt einen Projektwert in die Schreibweise des XTF-Modells.
    ///
    /// <c>Funktion</c> und <c>Material</c> fuehren eigene Vokabulare: <c>Normschacht</c>
    /// kennt beim Material nur vier Werte (andere, Beton, Kunststoff, unbekannt) — eine
    /// viel kuerzere Liste als beim Rohr. Die Rohrliste hier anzuwenden war der Fehler
    /// des AWU-Exporters: 2138 von 2500 gesichteten Schaechten tragen damit
    /// <c>Beton_unbekannt</c> oder <c>Kunststoff_unbekannt</c>, Werte, die
    /// <c>Normschacht.Material</c> gar nicht kennt.
    ///
    /// <c>unbekannt</c> ist keine Angabe und wird nicht geschrieben — sonst wuerde ein
    /// vorhandener besserer Wert in der Datei durch eine Leerformel ersetzt.
    /// </summary>
    public static string? NachXtfWert(string xtfName, string projektWert)
    {
        var wert = (projektWert ?? "").Trim();
        if (wert.Length == 0)
            return null;

        // Die Bemerkung geht bewusst VOR der "unbekannt"-Regel heraus: Bei Funktion und
        // Material ist "unbekannt" eine Leerformel, in einem Freitext dagegen eine
        // echte Aussage ("Material unbekannt, Deckel nicht zu oeffnen").
        if (string.Equals(xtfName, "Bemerkung", StringComparison.Ordinal))
            return XtfStammdatenPlanBuilder.AlsBemerkung(wert);

        var norm = xtfName switch
        {
            "Funktion" => SchachtFunktionVokabular.NachNorm(wert),
            "Material" => SchachtMaterialVokabular.NachNorm(wert),
            "BaulicherZustand" => XtfStammdatenPlanBuilder.NachXtfWert("BaulicherZustand", wert),
            _ => null
        };

        return string.Equals(norm, "unbekannt", StringComparison.OrdinalIgnoreCase) ? null : norm;
    }

    /// <summary>
    /// Die beiden Masse aus dem einen Textfeld des Programms, in Millimetern.
    ///
    /// Gelesen werden "600 mm" (rund) und "1100 x 900 mm" (eckig) — genau die zwei
    /// Schreibweisen, die der PDF- und der XTF-Import erzeugen. Beim runden Schacht
    /// traegt die Norm denselben Wert in beiden Feldern; so steht es auch im
    /// Kantonsexport. <c>null</c> heisst: keine brauchbare Angabe, es wird nichts
    /// geschrieben.
    /// </summary>
    public static (string Dimension1, string Dimension2)? Abmessungen(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;

        var teile = text.Split(['x', 'X', '×'], StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length > 2)
            return null;

        var erstes = SiaAbmessung.NachMillimeter(teile[0]);
        if (erstes is not > 0)
            return null;

        if (teile.Length == 1)
        {
            var rund = erstes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return (rund, rund);
        }

        var zweites = SiaAbmessung.NachMillimeter(teile[1]);
        if (zweites is not > 0)
            return null;

        return (
            erstes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            zweites.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <param name="buch">
    /// Das gemeinsame Organisationsbuch der Datei. Haltungen und Schaechte muessen
    /// dasselbe benutzen, sonst entstehen dieselbe Organisation zweimal oder zwei
    /// Objekte mit derselben Kennung.
    /// </param>
    public static XtfStammdatenPlan Build(
        IEnumerable<SchachtRecord> schaechte,
        IReadOnlyList<XtfStammdatenElement> elemente,
        XtfOrganisationsbuch? buch = null)
    {
        ArgumentNullException.ThrowIfNull(schaechte);
        ArgumentNullException.ThrowIfNull(elemente);

        var positionen = new List<XtfRevisionPosition>();
        var hinweise = new List<string>();
        buch ??= new XtfOrganisationsbuch(elemente);

        var normschaechte = BaueIndex(elemente);
        if (normschaechte.Count == 0)
            return new XtfStammdatenPlan(positionen, hinweise, buch.Neue);

        foreach (var record in schaechte)
        {
            var nummer = (Wert(record, Nummernfeld) ?? "").Trim();
            if (nummer.Length == 0)
                continue;

            if (!normschaechte.TryGetValue(nummer, out var element))
            {
                if (HatHandaenderung(record))
                    hinweise.Add($"Schacht {nummer}: in der XTF nicht gefunden — die Handaenderung bleibt aussen vor.");
                continue;
            }

            // Eine doppelte Bezeichnung ist nicht eindeutig; dann wird nichts zugeordnet.
            if (element is null)
            {
                if (HatHandaenderung(record))
                    hinweise.Add($"Schacht {nummer}: kommt in der XTF mehrfach vor — nicht eindeutig, nichts geschrieben.");
                continue;
            }

            var felder = SammleFelder(record, element, nummer, hinweise);

            var eigentuemer = buch.Verweis(
                element,
                $"Schacht {nummer}",
                IstHandgesetzt(record, FieldKeys.Owner) ? Wert(record, FieldKeys.Owner) : null,
                hinweise);
            if (eigentuemer is not null)
                felder.Insert(0, eigentuemer);

            if (felder.Count == 0)
                continue;

            positionen.Add(new XtfRevisionPosition(
                XtfRevisionAenderung.Geaendert,
                element.Tid,
                UntersuchungTid: "",
                nummer,
                Code: "",
                Meter: null,
                felder));
        }

        return new XtfStammdatenPlan(positionen, hinweise, buch.Neue);
    }

    private static List<XtfRevisionFeld> SammleFelder(
        SchachtRecord record,
        XtfStammdatenElement element,
        string nummer,
        List<string> hinweise)
    {
        var felder = new List<XtfRevisionFeld>();

        foreach (var (xtfName, projektFeld) in Felder)
        {
            if (!record.IsUserEdited(projektFeld))
                continue;

            var roh = (Wert(record, projektFeld) ?? "").Trim();
            var neu = NachXtfWert(xtfName, roh);
            if (string.IsNullOrEmpty(neu))
            {
                if (roh.Length > 0)
                {
                    hinweise.Add(
                        string.Equals(xtfName, "Bemerkung", StringComparison.Ordinal)
                        && XtfStammdatenPlanBuilder.BemerkungZuLang(roh, out var zeichen)
                            ? $"Schacht {nummer}: die Bemerkung ist {zeichen} Zeichen lang, das " +
                              $"Modell laesst {XtfStammdatenPlanBuilder.BemerkungGrenze} zu — nicht geschrieben."
                            : $"Schacht {nummer}: {xtfName} = \"{roh}\" passt zu keinem gueltigen " +
                              "Wert nach SIA405 — nicht geschrieben.");
                }

                continue;
            }

            Ergaenze(xtfName, neu);
        }

        if (record.IsUserEdited(Dimensionsfeld))
        {
            var roh = (Wert(record, Dimensionsfeld) ?? "").Trim();
            var masse = Abmessungen(roh);
            if (masse is null)
            {
                if (roh.Length > 0)
                    hinweise.Add($"Schacht {nummer}: die Dimension \"{roh}\" ist nicht lesbar — nicht geschrieben.");
            }
            else
            {
                Ergaenze("Dimension1", masse.Value.Dimension1);
                Ergaenze("Dimension2", masse.Value.Dimension2);
            }
        }

        return felder;

        void Ergaenze(string xtfName, string neu)
        {
            element.Werte.TryGetValue(xtfName, out var alt);
            alt = (alt ?? "").Trim();
            if (string.Equals(alt, neu, StringComparison.Ordinal))
                return;

            felder.Add(new XtfRevisionFeld(xtfName, alt.Length == 0 ? null : alt, neu));
        }
    }

    /// <summary>
    /// Nachschlagewerk ueber die Schachtnummer. Eine doppelte Bezeichnung wird zu
    /// <c>null</c> — nicht eindeutig heisst: nichts zuordnen. Anders als bei den
    /// Haltungen gibt es hier keine Gegenrichtung; ein Schacht hat genau einen Namen.
    /// </summary>
    private static Dictionary<string, XtfStammdatenElement?> BaueIndex(
        IReadOnlyList<XtfStammdatenElement> elemente)
    {
        var index = new Dictionary<string, XtfStammdatenElement?>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in elemente)
        {
            if (!string.Equals(element.Klasse, "Normschacht", StringComparison.Ordinal))
                continue;

            var name = (element.Bezeichnung ?? "").Trim();
            if (name.Length == 0)
                continue;

            if (!index.TryAdd(name, element))
                index[name] = null;
        }

        return index;
    }

    private static bool HatHandaenderung(SchachtRecord record)
    {
        foreach (var projektFeld in Felder.Values)
        {
            if (IstHandgesetzt(record, projektFeld) && !string.IsNullOrWhiteSpace(Wert(record, projektFeld)))
                return true;
        }

        return (IstHandgesetzt(record, Dimensionsfeld) && !string.IsNullOrWhiteSpace(Wert(record, Dimensionsfeld)))
            || (IstHandgesetzt(record, FieldKeys.Owner) && !string.IsNullOrWhiteSpace(Wert(record, FieldKeys.Owner)));
    }
}
