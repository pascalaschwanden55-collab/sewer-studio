using System.Text;
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
    IReadOnlyList<string> Hinweise,
    IReadOnlyList<XtfNeueOrganisation>? NeueOrganisationen = null)
{
    /// <summary>Organisationen, die es fuer einen Eigentuemer noch nicht gibt.</summary>
    public IReadOnlyList<XtfNeueOrganisation> Organisationen
        => NeueOrganisationen ?? Array.Empty<XtfNeueOrganisation>();
}

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
    /// Alle haengen an der SIA405-Klasse "Kanal"; <c>BaulicherZustand</c> und
    /// <c>Bemerkung</c> sind von der Oberklasse "Abwasserbauwerk" geerbt.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Felder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Nutzungsart_Ist"] = FieldKeys.UsageType,
            ["BaulicherZustand"] = FieldKeys.ConditionClass,
            ["FunktionHierarchisch"] = FieldKeys.HierarchicalFunction,
            ["Verbindungsart"] = FieldKeys.ConnectionType,
            ["Bettung_Umhuellung"] = FieldKeys.BeddingEncasement,
            ["FunktionHydraulisch"] = FieldKeys.HydraulicFunction,
            ["Status"] = FieldKeys.OperatingStatus,
            ["Sanierungsbedarf"] = FieldKeys.RehabilitationNeed,
            ["Baujahr"] = FieldKeys.ConstructionYear,
            ["Bruttokosten"] = FieldKeys.GrossCost,
            ["Bemerkung"] = FieldKeys.Remarks
        };

    /// <summary>
    /// Die Felder der Kataster-Infobox OHNE Ziel in der Revision. Sie bleiben im
    /// Programm sichtbar und bearbeitbar, gehen aber nie in die Datei zurueck.
    ///
    /// <c>Strasse</c> haette mit <c>Kanal.Standortname</c> zwar ein Ziel, wird auf
    /// ausdrueckliche Anweisung trotzdem nicht geschrieben (2026-09-02).
    /// <c>Lichte_Breite_mm</c> steht NICHT mehr hier: Es hat zwar kein direktes Feld,
    /// geht aber seit 2026-09-03 als <c>HoehenBreitenverhaeltnis</c> ans Rohrprofil
    /// (siehe <see cref="XtfRohrprofilVerhaeltnis"/>).
    /// Die sechs Herkunftsangaben sind der Nachweis, woher ein Datensatz stammt —
    /// keine Aussage von SewerStudio: Der Datenherr einer Kantonsleitung ist der
    /// Kanton, nicht der Operateur, und <c>Letzte_Aenderung</c> fuehrt der Schreiber
    /// dort selbst nach, wo die Datei das Feld kennt.
    ///
    /// Die Liste steht hier, damit der Verzicht sichtbar bleibt und nicht als
    /// vergessene Luecke wieder eingebaut wird; ein Test haelt sie fest.
    /// </summary>
    public static readonly IReadOnlyList<string> NichtExportierteFelder =
    [
        FieldKeys.Street,
        FieldKeys.CadastreObjectId,
        FieldKeys.DataOwner,
        FieldKeys.DataSupplier,
        FieldKeys.CadastreOrganisation,
        FieldKeys.CadastreLastChange,
        FieldKeys.CadastreUpdatedAt
    ];

    /// <summary>
    /// <c>Strasse</c> hat in SIA405 mit <c>Kanal.Standortname</c> zwar ein Ziel, wird
    /// aber auf ausdrueckliche Anweisung NICHT mehr geschrieben (2026-09-02). Das Feld
    /// bleibt im Programm vollstaendig erhalten — es geht nur nicht mehr in die Revision.
    /// Der Eintrag steht hier, damit der Verzicht sichtbar bleibt und nicht als
    /// vergessene Zeile wieder eingebaut wird.
    /// </summary>
    public const string NichtExportiert = FieldKeys.Street;

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
            ["Lichte_Hoehe"] = FieldKeys.NominalDiameterMm,
            ["LaengeEffektiv"] = FieldKeys.HoldingLengthMeters,
            ["Lagebestimmung"] = FieldKeys.PositionAccuracy
        };

    /// <summary>
    /// Bei einem widerspruechlichen Wechsel auf Kreis bleibt nur die Abmessung draussen.
    /// Unabhaengige Haltungswerte wie Material oder Laenge duerfen weiterhin mitgehen.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> HaltungFelderOhneAbmessung =
        HaltungFelder
            .Where(feld => !string.Equals(feld.Key, "Lichte_Hoehe", StringComparison.Ordinal))
            .ToDictionary(feld => feld.Key, feld => feld.Value, StringComparer.Ordinal);

    /// <summary>
    /// Abbildung XTF-Element -> Projektfeld fuer die Klasse "Rohrprofil".
    ///
    /// Der Profiltyp haengt nicht an der Haltung, sondern an einem eigenen Objekt, auf
    /// das die Haltung ueber <c>RohrprofilRef</c> zeigt. Im Kantonsexport von Abwasser Uri
    /// besitzt jede der 109871 Haltungen ihr eigenes Rohrprofil (109871 Objekte, 1:1).
    /// Verlassen wird sich darauf trotzdem nicht: Ein von mehreren Haltungen benutztes
    /// Profil wird nicht geaendert, weil die Aenderung sonst fremde Haltungen traefe.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RohrprofilFelder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Profiltyp"] = FieldKeys.ProfileType
        };

    /// <summary>Obergrenze aus <c>DOMAIN Lichte_Hoehe = 0 .. 99999 [Units.mm]</c>.</summary>
    private const int LichteHoeheMaxMm = 99999;

    /// <summary>Obergrenze aus <c>LaengeEffektiv: 0.00 .. 30000.00 [m]</c>.</summary>
    private const decimal LaengeMaxMeter = 30000.00m;

    /// <summary>Grenzen aus <c>DOMAIN Jahr = 1800 .. 2100</c> im Basismodell.</summary>
    private const int JahrMin = 1800;
    private const int JahrMax = 2100;

    /// <summary>Obergrenze aus <c>Bruttokosten: 0.00 .. 99999999.99 [Units.CHF]</c>.</summary>
    private const decimal BruttokostenMax = 99_999_999.99m;

    /// <summary>
    /// <c>Abwasserbauwerk.Bemerkung</c> ist im Modell <c>TEXT*80</c> — hoechstens
    /// achtzig Zeichen, und <c>TEXT</c> heisst in INTERLIS ausdruecklich einzeilig
    /// (mehrzeilig waere <c>MTEXT</c>, wie es daneben bei <c>Akten</c> steht).
    ///
    /// Die Grenze ist real: Im Kantonsexport ist die laengste Bemerkung exakt
    /// achtzig Zeichen lang und mitten im Wort abgeschnitten
    /// ("... Versickerungss"). Der Schreiber dort kappt also selbst.
    /// </summary>
    private const int BemerkungMaxZeichen = 80;

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

        if (string.Equals(xtfName, "Bemerkung", StringComparison.Ordinal))
            return AlsBemerkung(wert);

        if (string.Equals(xtfName, "Material", StringComparison.Ordinal))
            return MaterialVokabular.NachModell(wert, IstModell2020OderNeuer(modell));

        if (string.Equals(xtfName, "Lichte_Hoehe", StringComparison.Ordinal))
        {
            var mm = SiaAbmessung.NachMillimeter(wert);
            return mm is > 0 and <= LichteHoeheMaxMm
                ? mm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        // Die Haltungslaenge ist eine Laenge in Metern, keine Abmessung in Millimetern.
        // SiaAbmessung darf hier deshalb nie angewandt werden — aus 45,30 m wuerde 45300.
        // Gelesen wird ueber FachzahlParser, damit "45.30" und "45,30" gleich zaehlen.
        if (string.Equals(xtfName, "LaengeEffektiv", StringComparison.Ordinal))
        {
            if (!Common.FachzahlParser.TryParseMeasurement(wert, out var meter))
                return null;

            return meter is > 0 and <= LaengeMaxMeter
                ? meter.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        if (string.Equals(xtfName, "FunktionHierarchisch", StringComparison.Ordinal))
            return SiaKanalVokabular.FunktionHierarchisch.NachNorm(wert);

        if (string.Equals(xtfName, "Verbindungsart", StringComparison.Ordinal))
            return SiaKanalVokabular.Verbindungsart.NachNorm(wert);

        if (string.Equals(xtfName, "Bettung_Umhuellung", StringComparison.Ordinal))
            return SiaKanalVokabular.BettungUmhuellung.NachNorm(wert);

        if (string.Equals(xtfName, "Profiltyp", StringComparison.Ordinal))
            return ProfiltypVokabular.NachNorm(wert);

        if (string.Equals(xtfName, "FunktionHydraulisch", StringComparison.Ordinal))
            return SiaKanalVokabular.FunktionHydraulisch.NachNorm(wert);

        if (string.Equals(xtfName, "Status", StringComparison.Ordinal))
            return SiaKanalVokabular.Status.NachNorm(wert);

        if (string.Equals(xtfName, "Sanierungsbedarf", StringComparison.Ordinal))
            return SiaKanalVokabular.Sanierungsbedarf.NachNorm(wert);

        if (string.Equals(xtfName, "Lagebestimmung", StringComparison.Ordinal))
            return SiaKanalVokabular.Lagebestimmung.NachNorm(wert);

        // Baujahr: ganze Jahreszahl im Bereich der Norm (DOMAIN Jahr = 1800 .. 2100).
        if (string.Equals(xtfName, "Baujahr", StringComparison.Ordinal))
        {
            return int.TryParse(wert, System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture, out var jahr)
                   && jahr is >= JahrMin and <= JahrMax
                ? jahr.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        // Bruttokosten: Franken mit zwei Stellen, 0.00 bis 99999999.99. Ueber den
        // FachzahlParser, damit "1250.00" und "1'250.00" gleich zaehlen.
        if (string.Equals(xtfName, "Bruttokosten", StringComparison.Ordinal))
        {
            if (!Common.FachzahlParser.TryParseDecimal(wert, out var franken))
                return null;

            return franken is >= 0 and <= BruttokostenMax
                ? franken.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }

        if (!string.Equals(xtfName, "Nutzungsart_Ist", StringComparison.Ordinal))
            return wert;

        return NutzungsartVokabular.NachModell(wert, IstModell2020OderNeuer(modell));
    }

    /// <summary>
    /// Eine Bemerkung fuer <c>TEXT*80</c>, oder <c>null</c>, wenn sie nicht hineinpasst.
    ///
    /// Umbrueche und Tabulatoren werden zu Leerzeichen und mehrfache Leerzeichen
    /// zusammengezogen: <c>TEXT</c> ist einzeilig, und ein Umbruch traegt keine Aussage,
    /// die dabei verloren ginge.
    ///
    /// Ueberlaenge dagegen wird NICHT gekuerzt, sondern abgelehnt. Kuerzen verliert
    /// Inhalt, und zwar unsichtbar — der Nutzer saehe im Programm den ganzen Satz und in
    /// der Datei den halben. Der Bericht nennt stattdessen Haltung und Zeichenzahl,
    /// damit die Kuerzung dort entsteht, wo jemand den Sinn kennt.
    /// </summary>
    public static string? AlsBemerkung(string? text)
    {
        var einzeilig = new StringBuilder((text ?? "").Length);
        var letzteWarLeer = true;

        foreach (var zeichen in text ?? "")
        {
            var ist = char.IsWhiteSpace(zeichen) ? ' ' : zeichen;
            if (ist == ' ')
            {
                if (letzteWarLeer)
                    continue;

                letzteWarLeer = true;
            }
            else
            {
                letzteWarLeer = false;
            }

            einzeilig.Append(ist);
        }

        var fertig = einzeilig.ToString().TrimEnd();
        return fertig.Length is > 0 and <= BemerkungMaxZeichen ? fertig : null;
    }

    /// <summary>
    /// True, wenn eine Bemerkung allein an der Laengengrenze des Modells scheitert.
    /// Dann sagt der Bericht das auch so, statt einen unbekannten Begriff zu vermuten.
    /// Haltung und Schacht teilen sich diese Pruefung — beide erben das Feld von
    /// <c>Abwasserbauwerk</c> und damit dieselbe Grenze.
    /// </summary>
    public static bool BemerkungZuLang(string? roh, out int zeichen)
    {
        zeichen = (roh ?? "").Trim().Length;
        return zeichen > BemerkungMaxZeichen;
    }

    /// <summary>Die Zeichengrenze des Modells fuer <c>Bemerkung</c>.</summary>
    public static int BemerkungGrenze => BemerkungMaxZeichen;

    private static bool IstZuLang(string xtfName, string roh, out int zeichen)
    {
        zeichen = 0;
        return string.Equals(xtfName, "Bemerkung", StringComparison.Ordinal)
            && BemerkungZuLang(roh, out zeichen);
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
        string? modell = null,
        XtfOrganisationsbuch? buch = null)
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
        var profile = BaueProfilindex(elemente);
        buch ??= new XtfOrganisationsbuch(elemente);

        foreach (var record in haltungen)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            if (name.Length == 0)
                continue;

            var kanal = kanaele.Finde(name);
            var haltungElement = haltungenXtf.Finde(name);

            if (kanal is null && haltungElement is null)
            {
                if (HatHandaenderung(record, Felder)
                    || HatHandaenderung(record, HaltungFelder)
                    || HatHandaenderung(record, RohrprofilFelder)
                    || HatHandaenderungAmVerhaeltnis(record)
                    || HatHandaenderung(record, EigentuemerFeldKarte))
                {
                    hinweise.Add($"{name}: in der XTF nicht gefunden — die Handaenderung bleibt aussen vor.");
                }

                continue;
            }

            var eigentuemer = buch.Verweis(
                kanal,
                name,
                record.FieldMeta.TryGetValue(FieldKeys.Owner, out var eigentuemerMeta)
                    && eigentuemerMeta.UserEdited
                        ? record.GetFieldValue(FieldKeys.Owner)
                        : null,
                hinweise);
            var konfliktHoehe = "";
            var konfliktBreite = "";
            var profilMassKonflikt = ProfiltypWirdKreis(record)
                                     && HatZweiVerschiedeneMasse(record, out konfliktHoehe, out konfliktBreite);
            SammlePosition(record, kanal, Felder, name, modell, positionen, hinweise, eigentuemer);
            SammlePosition(
                record,
                haltungElement,
                profilMassKonflikt ? HaltungFelderOhneAbmessung : HaltungFelder,
                name,
                modell,
                positionen,
                hinweise);

            if (profilMassKonflikt)
            {
                hinweise.Add(
                    $"{name}: Kreisprofil mit zwei verschiedenen Massen ({konfliktHoehe} x {konfliktBreite}) — " +
                    "Abmessung und Rohrprofil werden nicht geaendert.");
            }
            else
            {
                var profil = FindeProfil(haltungElement, profile, name, record, hinweise);
                if (profil is not null)
                {
                    SammlePosition(
                        record, profil, RohrprofilFelder, name, modell, positionen, hinweise,
                        VerhaeltnisFeld(record, profil, name, hinweise));
                }
            }
        }

        return new XtfStammdatenPlan(positionen, hinweise, buch.Neue);
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
        List<string> hinweise,
        XtfRevisionFeld? zusatz = null)
    {
        if (element is null)
        {
            if (HatHandaenderung(record, felderKarte))
                hinweise.Add($"{name}: in der XTF nicht gefunden — die Handaenderung bleibt aussen vor.");
            return;
        }

        var felder = new List<XtfRevisionFeld>();
        if (zusatz is not null)
            felder.Add(zusatz);
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
                    hinweise.Add(IstZuLang(xtfName, roh, out var zeichen)
                        ? $"{name}: die Bemerkung ist {zeichen} Zeichen lang, das Modell " +
                          $"laesst {BemerkungMaxZeichen} zu — nicht geschrieben."
                        : $"{name}: {xtfName} = \"{roh}\" passt in dieser XTF zu keinem " +
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
            felder,
            Objekt: "Haltung"));
    }

    /// <summary>Der Eigentuemer als eigene Feldkarte — er laeuft nicht ueber den Textweg.</summary>
    internal static readonly IReadOnlyDictionary<string, string> EigentuemerFeldKarte =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EigentuemerRef"] = FieldKeys.Owner
        };

    /// <summary>
    /// Die Rohrprofile der Datei, nachschlagbar ueber ihre Kennung, samt der Zahl der
    /// Haltungen, die auf sie zeigen.
    /// </summary>
    private sealed record Profilindex(
        IReadOnlyDictionary<string, XtfStammdatenElement> JeTid,
        IReadOnlyDictionary<string, int> Verweise);

    private static Profilindex BaueProfilindex(IReadOnlyList<XtfStammdatenElement> elemente)
    {
        var jeTid = new Dictionary<string, XtfStammdatenElement>(StringComparer.Ordinal);
        var verweise = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var element in elemente)
        {
            if (string.Equals(element.Klasse, "Rohrprofil", StringComparison.Ordinal))
            {
                jeTid.TryAdd(element.Tid, element);
                continue;
            }

            if (!string.Equals(element.Klasse, "Haltung", StringComparison.Ordinal))
                continue;

            if (!element.Werte.TryGetValue("RohrprofilRef", out var referenz))
                continue;

            var tid = (referenz ?? "").Trim();
            if (tid.Length == 0)
                continue;

            verweise[tid] = verweise.TryGetValue(tid, out var bisher) ? bisher + 1 : 1;
        }

        return new Profilindex(jeTid, verweise);
    }

    /// <summary>
    /// Das Rohrprofil einer Haltung, oder <c>null</c>.
    ///
    /// Fail-closed an drei Stellen: ohne Haltungsobjekt, ohne Verweis und bei einem von
    /// mehreren Haltungen geteilten Profil wird nichts geaendert. Ein geteiltes Profil zu
    /// aendern wuerde fremde Haltungen mit umschreiben — im Kantonsexport liegt zwar
    /// je Haltung ein eigenes Profil, andere Lieferungen duerfen das aber anders halten.
    ///
    /// Gemeldet wird nur, wenn der Mensch am Profiltyp ueberhaupt etwas geaendert hat.
    /// </summary>
    /// <summary>
    /// Die zwei Programmfelder, aus denen das Hoehen-Breiten-Verhaeltnis des Rohrprofils
    /// entsteht. Beide zaehlen als Handaenderung am Profil: Wer die Breite oder die
    /// Hoehe korrigiert, aendert das Verhaeltnis mit.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> VerhaeltnisFelder =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [XtfRohrprofilVerhaeltnis.Attribut] = FieldKeys.ClearWidthMm,
            ["Lichte_Hoehe"] = FieldKeys.NominalDiameterMm
        };

    /// <summary>
    /// Das Hoehen-Breiten-Verhaeltnis als Aenderung am Rohrprofil, oder <c>null</c>,
    /// wenn nichts zu schreiben ist. Eine bewusst auf leer oder gleich gesetzte Breite
    /// entfernt ein vorhandenes altes Verhaeltnis ausdruecklich. Dasselbe gilt fuer einen
    /// eindeutigen Profilwechsel auf Kreis. Ungueltige Masse, derselbe Wert wie in der
    /// Datei oder ein Widerspruch mit dem Kreisprofil werden nicht geschrieben.
    /// </summary>
    private static XtfRevisionFeld? VerhaeltnisFeld(
        HaltungRecord record,
        XtfStammdatenElement profil,
        string name,
        List<string> hinweise)
    {
        var profiltypWirdKreis = ProfiltypWirdKreis(record);
        if (!HatHandaenderungAmVerhaeltnis(record) && !profiltypWirdKreis)
            return null;

        var hoehe = record.GetFieldValue(FieldKeys.NominalDiameterMm);
        var breite = record.GetFieldValue(FieldKeys.ClearWidthMm);
        var neu = XtfRohrprofilVerhaeltnis.Berechne(hoehe, breite);

        profil.Werte.TryGetValue(XtfRohrprofilVerhaeltnis.Attribut, out var alt);
        alt = (alt ?? "").Trim();
        if (neu is null)
        {
            if ((!BreiteWurdeBewusstAufRundGesetzt(record, hoehe, breite) && !profiltypWirdKreis)
                || alt.Length == 0)
                return null;

            return new XtfRevisionFeld(
                XtfRohrprofilVerhaeltnis.Attribut,
                alt,
                Neu: null,
                Aktion: XtfRevisionFeldAktion.Entfernen);
        }

        var profiltypVonHand = record.FieldMeta.TryGetValue(FieldKeys.ProfileType, out var profilMeta)
                               && profilMeta.UserEdited;
        var profiltyp = profiltypVonHand
            ? NachXtfWert("Profiltyp", record.GetFieldValue(FieldKeys.ProfileType) ?? "")
            : (profil.Werte.TryGetValue("Profiltyp", out var ausDatei) ? ausDatei : null);
        if (string.Equals((profiltyp ?? "").Trim(), "Kreisprofil", StringComparison.Ordinal))
        {
            hinweise.Add(
                $"{name}: Kreisprofil mit zwei verschiedenen Massen ({hoehe} x {breite}) — " +
                "das Hoehen-Breiten-Verhaeltnis wird nicht geschrieben.");
            return null;
        }

        return XtfRohrprofilVerhaeltnis.Gleich(alt, neu)
            ? null
            : new XtfRevisionFeld(XtfRohrprofilVerhaeltnis.Attribut, alt.Length == 0 ? null : alt, neu);
    }

    private static XtfStammdatenElement? FindeProfil(
        XtfStammdatenElement? haltungElement,
        Profilindex profile,
        string name,
        HaltungRecord record,
        List<string> hinweise)
    {
        // Zwei verschiedene Masse setzen ein Verhaeltnis. Rund (gleiche Masse oder eine
        // bewusst geleerte Breite) muss das vorhandene Verhaeltnis entfernen koennen.
        // Ungueltige Masse duerfen dagegen weder schreiben noch loeschen.
        var hoehe = record.GetFieldValue(FieldKeys.NominalDiameterMm);
        var breite = record.GetFieldValue(FieldKeys.ClearWidthMm);
        var verhaeltnisNoetig = HatHandaenderungAmVerhaeltnis(record)
                                && (XtfRohrprofilVerhaeltnis.Berechne(hoehe, breite) is not null
                                    || BreiteWurdeBewusstAufRundGesetzt(record, hoehe, breite));
        if (!HatHandaenderung(record, RohrprofilFelder) && !verhaeltnisNoetig)
            return null;

        if (haltungElement is null
            || !haltungElement.Werte.TryGetValue("RohrprofilRef", out var referenz)
            || string.IsNullOrWhiteSpace(referenz))
        {
            hinweise.Add($"{name}: die XTF fuehrt kein Rohrprofil — der Profiltyp bleibt aussen vor.");
            return null;
        }

        var tid = referenz.Trim();
        if (!profile.JeTid.TryGetValue(tid, out var profil))
        {
            hinweise.Add($"{name}: das verwiesene Rohrprofil {tid} fehlt in der Datei — Profiltyp nicht geschrieben.");
            return null;
        }

        if (profile.Verweise.TryGetValue(tid, out var anzahl) && anzahl > 1)
        {
            hinweise.Add(
                $"{name}: das Rohrprofil {tid} wird von {anzahl} Haltungen gemeinsam benutzt — " +
                "der Profiltyp wird nicht geaendert, weil das die uebrigen mit umschreiben wuerde.");
            return null;
        }

        return profil;
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

    /// <summary>
    /// Beim Profilverhaeltnis ist auch das bewusste Leeren der Breite eine Aenderung:
    /// zusammen mit einer gueltigen Hoehe bedeutet es "rund". Die allgemeine Feldpruefung
    /// ignoriert leere Werte absichtlich und darf deshalb hier nicht verwendet werden.
    /// </summary>
    private static bool HatHandaenderungAmVerhaeltnis(HaltungRecord record)
        => VerhaeltnisFelder.Values.Any(projektFeld =>
            record.FieldMeta.TryGetValue(projektFeld, out var meta) && meta.UserEdited);

    /// <summary>
    /// Eine leere oder gleiche Breite bedeutet nur dann einen bewussten Wechsel auf rund,
    /// wenn genau dieses Breitenfeld vom Menschen bearbeitet wurde. Eine allein geaenderte
    /// Hoehe darf eine bloss fehlende importierte Breite nie als Loeschauftrag umdeuten.
    /// </summary>
    private static bool BreiteWurdeBewusstAufRundGesetzt(
        HaltungRecord record,
        string? hoehe,
        string? breite)
        => record.FieldMeta.TryGetValue(FieldKeys.ClearWidthMm, out var meta)
           && meta.UserEdited
           && XtfRohrprofilVerhaeltnis.IstRund(hoehe, breite);

    private static bool ProfiltypWirdKreis(HaltungRecord record)
        => record.FieldMeta.TryGetValue(FieldKeys.ProfileType, out var meta)
           && meta.UserEdited
           && string.Equals(
               NachXtfWert("Profiltyp", record.GetFieldValue(FieldKeys.ProfileType) ?? ""),
               "Kreisprofil",
               StringComparison.Ordinal);

    private static bool HatZweiVerschiedeneMasse(
        HaltungRecord record,
        out string hoehe,
        out string breite)
    {
        hoehe = (record.GetFieldValue(FieldKeys.NominalDiameterMm) ?? "").Trim();
        breite = (record.GetFieldValue(FieldKeys.ClearWidthMm) ?? "").Trim();
        var h = SiaAbmessung.NachMillimeter(hoehe);
        var b = SiaAbmessung.NachMillimeter(breite);
        return h is > 0 and <= 99_999 && b is > 0 and <= 99_999 && h != b;
    }
}
