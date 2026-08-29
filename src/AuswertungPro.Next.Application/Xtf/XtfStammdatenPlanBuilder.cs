using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Ein Stammdaten-Objekt aus der SIA405-XTF mit seinen gelesenen Werten.
///
/// Zwei Klassen beschreiben dieselbe Leitung: <c>Kanal</c> fuehrt die logischen Angaben
/// (Nutzungsart, Standortname, Zustand), <c>Haltung</c> die physischen (Material, lichte
/// Hoehe, Laenge). Beide tragen dieselbe <c>Bezeichnung</c> — im Kantonsexport von
/// Abwasser Uri in allen 109871 Faellen identisch. <see cref="Klasse"/> haelt fest,
/// welche der beiden gelesen wurde, damit ein Feld nie an einem Objekt landet, dessen
/// Klasse es gar nicht kennt.
/// </summary>
public sealed record XtfStammdatenElement(
    string Tid,
    string Bezeichnung,
    IReadOnlyDictionary<string, string> Werte,
    string Klasse = "Kanal");

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
    /// Abbildung XTF-Element -> Projektfeld fuer die physische Klasse "Haltung".
    ///
    /// Getrennt von <see cref="Felder"/>, weil die Felder an einem anderen Objekt
    /// haengen: Im Kantonsexport von Abwasser Uri tragen alle 109871 Kanal-Objekte
    /// weder Material noch Lichte_Hoehe. Beide Klassen fuehren dieselbe Bezeichnung,
    /// die Zuordnung ueber den Haltungsnamen ist deshalb fuer beide dieselbe.
    ///
    /// Es sind genau die zwei Felder, die Pascal am haeufigsten von Hand korrigiert.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> HaltungFelder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Material"] = FieldKeys.PipeMaterial,
            ["Lichte_Hoehe"] = FieldKeys.NominalDiameterMm
        };

    /// <summary>Obergrenze aus <c>DOMAIN Lichte_Hoehe = 0 .. 99999 [Units.mm]</c>.</summary>
    private const int LichteHoeheMaxMm = 99999;

    /// <summary>
    /// Bringt einen Projektwert in die Schreibweise des XTF-Modells.
    ///
    /// <c>BaulicherZustand</c>: Das Projekt fuehrt die Zustandsklasse als blosse Ziffer,
    /// SIA405 verlangt <c>Z0</c> bis <c>Z4</c>. Beide zaehlen gleich herum — 0 ist der
    /// schlechteste Zustand, 4 bedeutet keine Maengel (VSA "Erhaltung von Kanalisationen").
    /// Es wird deshalb nur die Schreibweise angepasst, nichts umgerechnet.
    ///
    /// <c>Nutzungsart_Ist</c>: Die Begriffe fuehrt <see cref="NutzungsartVokabular"/>.
    /// Fuer das Regenwasser entscheidet die Modellfassung der Datei — SIA405 2015 kennt
    /// nur <c>Regenabwasser</c>, SIA405 2020 nur <c>Niederschlagsabwasser</c>.
    ///
    /// <c>Material</c>: Die Begriffe fuehrt <see cref="MaterialVokabular"/>, ebenfalls
    /// modellabhaengig. Die 2015-Fassung kennt die Kategorie-Praefixe nicht
    /// (<c>Polyethylen</c> statt <c>Kunststoff_Polyethylen</c>) und ist zudem groeber.
    /// Wo keine 2015-Schreibweise belegt ist, wird nichts geschrieben.
    ///
    /// <c>Lichte_Hoehe</c>: Millimeter als ganze Zahl, Wertebereich 0 bis 99999 laut
    /// <c>SIA405_Abwasser_2020_2_d_LV95</c>. Die Null bedeutet in dieser Datei
    /// "unbekannt" und ist keine Angabe; sie wird deshalb nicht geschrieben.
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

        if (string.Equals(xtfName, "Material", StringComparison.Ordinal))
            return MaterialVokabular.NachModell(wert, IstModell2020OderNeuer(modell));

        if (string.Equals(xtfName, "Lichte_Hoehe", StringComparison.Ordinal))
        {
            var mm = SiaAbmessung.NachMillimeter(wert);
            return mm is > 0 and <= LichteHoeheMaxMm
                ? mm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        if (!string.Equals(xtfName, "Nutzungsart_Ist", StringComparison.Ordinal))
            return wert;

        return NutzungsartVokabular.NachModell(wert, IstModell2020OderNeuer(modell));
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

        // Je Klasse ein eigener Index: Kanal und Haltung tragen dieselbe Bezeichnung,
        // sind aber verschiedene Objekte mit verschiedenen Feldern.
        var kanaele = BaueIndex(elemente, "Kanal");
        var haltungenXtf = BaueIndex(elemente, "Haltung");

        foreach (var record in haltungen)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0)
                continue;

            var kanal = kanaele.Finde(name);
            var haltungElement = haltungenXtf.Finde(name);

            if (kanal is null && haltungElement is null)
            {
                if (HatHandaenderung(record, Felder) || HatHandaenderung(record, HaltungFelder))
                    hinweise.Add($"{name}: in der XTF nicht gefunden — die Handaenderung bleibt aussen vor.");
                continue;
            }

            SammlePosition(record, kanal, Felder, name, modell, positionen, hinweise);
            SammlePosition(record, haltungElement, HaltungFelder, name, modell, positionen, hinweise);
        }

        return new XtfStammdatenPlan(positionen, hinweise);
    }

    /// <summary>
    /// Traegt die Handaenderungen eines Datensatzes fuer genau ein XTF-Objekt zusammen.
    /// Fehlt das Objekt, wird eine gesetzte Handaenderung gemeldet statt still verworfen.
    /// </summary>
    private static void SammlePosition(
        HaltungRecord record,
        XtfStammdatenElement? element,
        IReadOnlyDictionary<string, string> felderKarte,
        string name,
        string? modell,
        List<XtfRevisionPosition> positionen,
        List<string> hinweise)
    {
        if (element is null)
        {
            if (HatHandaenderung(record, felderKarte))
                hinweise.Add($"{name}: in der XTF nicht gefunden — die Handaenderung bleibt aussen vor.");
            return;
        }

        var felder = new List<XtfRevisionFeld>();
        foreach (var (xtfName, projektFeld) in felderKarte)
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
            return;

        positionen.Add(new XtfRevisionPosition(
            XtfRevisionAenderung.Geaendert,
            element.Tid,
            UntersuchungTid: "",
            name,
            Code: "",
            Meter: null,
            felder));
    }

    /// <summary>
    /// Nachschlagewerk ueber die Bezeichnung einer Klasse, mit der Gegenrichtung als
    /// zweitem Weg. Doppelte Bezeichnungen sind nicht eindeutig und bleiben aussen vor.
    /// </summary>
    private sealed class Klassenindex
    {
        private readonly Dictionary<string, XtfStammdatenElement?> _direkt = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, XtfStammdatenElement?> _gedreht = new(StringComparer.OrdinalIgnoreCase);

        public void Fuege(string name, XtfStammdatenElement element)
        {
            if (!_direkt.TryAdd(name, element))
                _direkt[name] = null;

            var gedreht = Gegenrichtung(name);
            if (gedreht is not null && !_gedreht.TryAdd(gedreht, element))
                _gedreht[gedreht] = null;
        }

        /// <summary>Die Gegenrichtung greift nur, wenn es keinen direkten Treffer gibt.</summary>
        public XtfStammdatenElement? Finde(string name)
        {
            if (_direkt.TryGetValue(name, out var treffer) && treffer is not null)
                return treffer;

            return _gedreht.TryGetValue(name, out var gedreht) ? gedreht : null;
        }
    }

    private static Klassenindex BaueIndex(IReadOnlyList<XtfStammdatenElement> elemente, string klasse)
    {
        var index = new Klassenindex();
        foreach (var element in elemente)
        {
            if (!string.Equals(element.Klasse, klasse, StringComparison.Ordinal))
                continue;

            var name = (element.Bezeichnung ?? "").Trim();
            if (name.Length == 0)
                continue;

            index.Fuege(name, element);
        }

        return index;
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
    private static bool HatHandaenderung(HaltungRecord record, IReadOnlyDictionary<string, string> felderKarte)
    {
        foreach (var projektFeld in felderKarte.Values)
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
