using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>Was eine neu erzeugte XTF enthalten wuerde.</summary>
public sealed record XtfNeuPlan(
    IReadOnlyList<XtfNeuObjekt> Objekte,
    IReadOnlyList<string> Hinweise,
    int Haltungen,
    int Schaechte)
{
    public bool Leer => Haltungen == 0 && Schaechte == 0;
}

/// <summary>
/// Baut aus dem Projektstand die vollstaendige Objektliste einer NEUEN SIA405-XTF.
///
/// Das ist der Gegenpart zu <see cref="XtfStammdatenPlanBuilder"/>: Dort wird eine
/// vorhandene Kundendatei ergaenzt, hier entsteht eine eigenstaendige Datei aus dem
/// ganzen Projektstand. Eine einzelne vorhandene Objekt-ID reicht nicht fuer die TIDs
/// des ganzen SIA405-Objektverbunds und verhindert diesen Export deshalb nicht. Die
/// Datei erhaelt eigene, stabile SewerStudio-TIDs.
///
/// Eine Haltung ist in SIA405 kein einzelnes Objekt, sondern ein Verbund:
///
///   Kanal            die logischen Angaben (Nutzungsart, Zustand, Funktion, Eigentuemer)
///     ^ AbwasserbauwerkRef
///   Haltung          die physischen (Material, lichte Hoehe, Laenge, Verlauf)
///     +- RohrprofilRef        -> Rohrprofil
///     +- vonHaltungspunktRef  -> Haltungspunkt   } beide PFLICHT {1}
///     +- nachHaltungspunktRef -> Haltungspunkt   }
///                                    v AbwassernetzelementRef
///                                Abwasserknoten -> Normschacht
///
/// Drei Verweise sind im Modell Pflicht ({1}) und tragen deshalb den ganzen Bau:
/// <c>DatenherrRef</c>, <c>DatenlieferantRef</c> und am Abwasserbauwerk zusaetzlich
/// <c>EigentuemerRef</c>. Ohne bekannten Eigentuemer entsteht das Objekt nicht — eine
/// erfundene Organisation waere eine Aussage, die niemand getroffen hat.
///
/// Reine Rechnung: kein Dateizugriff, keine Mutation des Projekts.
/// </summary>
public static class XtfNeuPlanBuilder
{
    /// <summary>Der Haltungspunkt fuehrt seine Bezeichnung als <c>TEXT*20</c>.</summary>
    private const int HaltungspunktNameMax = 20;

    /// <summary>Kanal, Haltung und Normschacht fuehren sie als <c>TEXT*41</c>.</summary>
    private const int BauwerkNameMax = 41;

    /// <summary>Die Projektfelder mit den Namen der beiden Nachbarschaechte.</summary>
    private const string SchachtObenFeld = "Schacht_oben";
    private const string SchachtUntenFeld = "Schacht_unten";

    public static XtfNeuPlan Build(
        IReadOnlyList<HaltungRecord>? haltungen,
        IReadOnlyList<SchachtRecord>? schaechte,
        string? projektKennung = null,
        IReadOnlyDictionary<string, XtfNeuGeometrie>? geometrien = null)
    {
        var hs = haltungen ?? [];
        var ss = schaechte ?? [];
        var hinweise = new List<string>();
        var objekte = new List<XtfNeuObjekt>();
        var kennungen = new XtfNeuKennungen(projektKennung);

        var organisationen = new Organisationsbuch(kennungen, objekte);
        var profile = new Profilbuch(kennungen, objekte);
        var punktnamen = new Punktnamen(HaltungspunktNameMax);
        // Schaechte zuerst: Die Haltungspunkte verweisen auf ihre Abwasserknoten.
        var knoten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var geschriebeneSchaechte = 0;
        foreach (var schacht in ss)
        {
            if (BaueSchacht(schacht, kennungen, organisationen, objekte, knoten, hinweise))
                geschriebeneSchaechte++;
        }

        var geschriebeneHaltungen = 0;
        foreach (var haltung in hs)
        {
            if (BaueHaltung(haltung, kennungen, organisationen, profile, punktnamen, objekte,
                            knoten, geometrien, hinweise))
                geschriebeneHaltungen++;
        }

        // Eine Organisation, die nur fuer ein wegen Pflichtfehlern verworfenes Objekt
        // angelegt wurde, darf nicht als verwaister Rest in der Datei stehen bleiben.
        var verwendeteTids = objekte
            .SelectMany(o => o.Verweise)
            .Select(v => v.ZielTid)
            .ToHashSet(StringComparer.Ordinal);
        objekte.RemoveAll(o =>
            o.Klasse == "Organisation" && !verwendeteTids.Contains(o.Tid));

        return new XtfNeuPlan(objekte, hinweise, geschriebeneHaltungen, geschriebeneSchaechte);
    }

    private static bool BaueHaltung(
        HaltungRecord record,
        XtfNeuKennungen kennungen,
        Organisationsbuch organisationen,
        Profilbuch profile,
        Punktnamen punktnamen,
        List<XtfNeuObjekt> objekte,
        IReadOnlyDictionary<string, string> knoten,
        IReadOnlyDictionary<string, XtfNeuGeometrie>? geometrien,
        List<string> hinweise)
    {
        var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        if (name.Length == 0)
        {
            hinweise.Add("Eine Haltung ohne Namen kann nicht geschrieben werden.");
            return false;
        }

        if (name.Length > BauwerkNameMax)
        {
            hinweise.Add(
                $"{name}: der Name ist {name.Length} Zeichen lang, das Modell laesst " +
                $"{BauwerkNameMax} zu — die Haltung wird nicht geschrieben.");
            return false;
        }

        // Das Feld kann eine lokale QGIS-ID oder die TID einer importierten Haltung
        // enthalten. Eine einzelne Kennung reicht nicht fuer Kanal, Haltungspunkte,
        // Knoten und Profil des ganzen Objektverbunds. Der eigenstaendige Export vergibt
        // deshalb durchgehend eigene chSST-TIDs und warnt vor einem Import in Bestand.
        // Traegt die Haltung ihre GEONIS-Kennungen (ueber "Katasterkennungen ergaenzen"
        // oder einen GEONIS-Import), schreibt der Export genau diese TIDs. Dann findet
        // der Kataster seine Objekte wieder, statt Duplikate anzulegen.
        var geonis = record.Geonis;
        var mitGeonis = geonis is not null && SiaObjektkennung.IstGueltig(geonis.Haltung);
        var objektId = (record.GetFieldValue(FieldKeys.CadastreObjectId) ?? "").Trim();
        if (mitGeonis)
        {
            hinweise.Add(
                $"{name}: GEONIS-Kennung {geonis!.Haltung} aus dem Kataster verwendet" +
                (geonis.RichtungGedreht
                    ? " — die Haltung heisst im Projekt in der Gegenrichtung zum Kataster."
                    : "."));
        }
        else if (objektId.Length > 0)
        {
            hinweise.Add(
                $"{name}: Objekt-ID {objektId} vorhanden — wird mit einer eigenen " +
                "XTF-Kennung exportiert. Fuer eine Aktualisierung im Kataster bitte " +
                "\"Revidierte XTF\" verwenden.");
        }

        var organisationsverweise = organisationen.Verweise(
            record.GetFieldValue(FieldKeys.Owner),
            record.GetFieldValue(FieldKeys.DataOwner),
            record.GetFieldValue(FieldKeys.DataSupplier),
            name,
            hinweise);
        if (organisationsverweise is null)
            return false;

        var eigentuemer = organisationsverweise.Value.Eigentuemer;
        var verwaltung = organisationsverweise.Value.Verwaltung;
        var profilTid = profile.Verweis(
            record.GetFieldValue(FieldKeys.ProfileType),
            Verhaeltnis(record, name, hinweise),
            verwaltung,
            mitGeonis ? geonis : null,
            name,
            hinweise);

        var geometrie = geometrien is not null && geometrien.TryGetValue(name, out var g) ? g : null;
        if (geometrie is null)
            hinweise.Add($"{name}: kein Verlauf gefunden — die Haltung geht ohne Geometrie hinaus.");

        var (vonTid, nachTid) = BaueHaltungspunkte(
            record, name, kennungen, verwaltung, objekte, knoten,
            geometrie, punktnamen, mitGeonis ? geonis : null, hinweise);

        var kanalTid = mitGeonis && SiaObjektkennung.IstGueltig(geonis!.Kanal)
            ? geonis.Kanal!
            : kennungen.Fuer("Kanal", name);
        objekte.Add(new XtfNeuObjekt(
            "Kanal", kanalTid,
            Sachfelder(record, name, XtfStammdatenPlanBuilder.Felder, hinweise),
            [.. verwaltung, new XtfNeuVerweis("EigentuemerRef", eigentuemer)]));

        var haltungTid = mitGeonis ? geonis!.Haltung! : kennungen.Fuer("Haltung", name);
        List<XtfNeuVerweis> haltungVerweise =
        [
            new("vonHaltungspunktRef", vonTid),
            new("nachHaltungspunktRef", nachTid),
            new("AbwasserbauwerkRef", kanalTid),
            .. verwaltung
        ];
        if (profilTid is not null)
            haltungVerweise.Insert(0, new XtfNeuVerweis("RohrprofilRef", profilTid));

        objekte.Add(new XtfNeuObjekt(
            "Haltung", haltungTid,
            Sachfelder(record, name, XtfStammdatenPlanBuilder.HaltungFelder, hinweise),
            haltungVerweise,
            geometrie));

        return true;
    }

    /// <summary>
    /// Die zwei Pflicht-Haltungspunkte, benannt nach der HALTUNG — nicht nach dem
    /// Schacht.
    ///
    /// Das ist keine Geschmacksfrage: <c>Haltungspunkt.Constraint1</c> verlangt, dass
    /// Bezeichnung und Datenherr zusammen eindeutig sind. In einer Kette 1-2, 2-3, 3-4
    /// teilen sich benachbarte Haltungen ihre Schaechte; der Schachtname kaeme dadurch
    /// mehrfach vor und der ilivalidator weist die ganze Datei ab. Genau deshalb benennt
    /// auch der Kantonsexport seine Haltungspunkte "&lt;Haltung&gt;_von" und "_nach".
    ///
    /// Die Bezeichnung ist <c>TEXT*20</c>. Passt der Name nicht hinein, wird er gekuerzt
    /// und notfalls durchnummeriert — sie ist ein technischer Hilfsname, und Eindeutigkeit
    /// wiegt hier schwerer als Lesbarkeit. Die fachliche Zuordnung zum Schacht traegt
    /// ohnehin der Verweis auf dessen Abwasserknoten, nicht der Text.
    /// </summary>
    private static (string Von, string Nach) BaueHaltungspunkte(
        HaltungRecord record,
        string name,
        XtfNeuKennungen kennungen,
        IReadOnlyList<XtfNeuVerweis> verwaltung,
        List<XtfNeuObjekt> objekte,
        IReadOnlyDictionary<string, string> knoten,
        XtfNeuGeometrie? verlauf,
        Punktnamen punktnamen,
        GeonisKennungen? geonis,
        List<string> hinweise)
    {
        var vonTid = EinHaltungspunkt(
            name, "von", (record.GetFieldValue(SchachtObenFeld) ?? "").Trim(),
            kennungen, verwaltung, objekte, knoten,
            verlauf?.Punkte.FirstOrDefault(), punktnamen,
            geonis?.VonPunkt, geonis?.VonPunktBezeichnung, hinweise);

        var nachTid = EinHaltungspunkt(
            name, "nach", (record.GetFieldValue(SchachtUntenFeld) ?? "").Trim(),
            kennungen, verwaltung, objekte, knoten,
            verlauf?.Punkte.LastOrDefault(), punktnamen,
            geonis?.NachPunkt, geonis?.NachPunktBezeichnung, hinweise);

        return (vonTid, nachTid);
    }

    private static string EinHaltungspunkt(
        string haltung,
        string seite,
        string schacht,
        XtfNeuKennungen kennungen,
        IReadOnlyList<XtfNeuVerweis> verwaltung,
        List<XtfNeuObjekt> objekte,
        IReadOnlyDictionary<string, string> knoten,
        XtfPunkt? lage,
        Punktnamen punktnamen,
        string? katasterTid,
        string? katasterName,
        List<string> hinweise)
    {
        // Mit GEONIS-Kennung traegt der Punkt auch den Namen des Katasters (z. B.
        // "A75394"): Derselbe Punkt, derselbe Name — sonst saehe der Import eine
        // Umbenennung, wo keine ist.
        var mitKataster = SiaObjektkennung.IstGueltig(katasterTid);
        var wunsch = mitKataster && !string.IsNullOrWhiteSpace(katasterName)
            ? katasterName.Trim()
            : $"{haltung}_{seite}";
        var bezeichnung = punktnamen.Eindeutig(wunsch);
        var tid = mitKataster ? katasterTid! : kennungen.Fuer("Haltungspunkt", $"{haltung}|{seite}");
        List<XtfNeuVerweis> verweise = [.. verwaltung];

        if (schacht.Length > 0 && knoten.TryGetValue(schacht, out var knotenTid))
            verweise.Insert(0, new XtfNeuVerweis("AbwassernetzelementRef", knotenTid));
        else if (schacht.Length > 0)
            hinweise.Add($"{haltung}: der Schacht \"{schacht}\" ist im Projekt nicht erfasst.");

        objekte.Add(new XtfNeuObjekt(
            "Haltungspunkt", tid,
            [new KeyValuePair<string, string>("Bezeichnung", bezeichnung)],
            verweise,
            lage is null ? null : new XtfNeuGeometrie("Lage", [lage])));

        return tid;
    }

    private static bool BaueSchacht(
        SchachtRecord record,
        XtfNeuKennungen kennungen,
        Organisationsbuch organisationen,
        List<XtfNeuObjekt> objekte,
        Dictionary<string, string> knoten,
        List<string> hinweise)
    {
        var nummer = (XtfSchachtPlanBuilder.Wert(record, XtfSchachtPlanBuilder.Nummernfeld) ?? "").Trim();
        if (nummer.Length == 0)
        {
            hinweise.Add("Ein Schacht ohne Nummer kann nicht geschrieben werden.");
            return false;
        }

        if (nummer.Length > BauwerkNameMax)
        {
            hinweise.Add(
                $"Schacht {nummer}: der Name ist {nummer.Length} Zeichen lang, das Modell " +
                $"laesst {BauwerkNameMax} zu — nicht geschrieben.");
            return false;
        }

        var geonis = record.Geonis;
        var mitGeonis = geonis is not null && SiaObjektkennung.IstGueltig(geonis.Knoten);
        var mitBauwerk = mitGeonis && SiaObjektkennung.IstGueltig(geonis!.Bauwerk);
        var objektId = (XtfSchachtPlanBuilder.Wert(record, FieldKeys.CadastreObjectId) ?? "").Trim();
        if (mitGeonis)
        {
            hinweise.Add(
                $"Schacht {nummer}: GEONIS-Kennung {geonis!.Knoten} aus dem Kataster verwendet" +
                (mitBauwerk ? "." : " — das Bauwerk hat dort keine Kennung und bekommt eine eigene."));
        }
        else if (objektId.Length > 0)
        {
            hinweise.Add(
                $"Schacht {nummer}: Objekt-ID {objektId} vorhanden — wird mit einer " +
                "eigenen XTF-Kennung exportiert. Fuer eine Aktualisierung im Kataster " +
                "bitte \"Revidierte XTF\" verwenden.");
        }

        var organisationsverweise = organisationen.Verweise(
            XtfSchachtPlanBuilder.Wert(record, FieldKeys.Owner),
            XtfSchachtPlanBuilder.Wert(record, FieldKeys.DataOwner),
            XtfSchachtPlanBuilder.Wert(record, FieldKeys.DataSupplier),
            $"Schacht {nummer}",
            hinweise);
        if (organisationsverweise is null)
            return false;

        var eigentuemer = organisationsverweise.Value.Eigentuemer;
        var verwaltung = organisationsverweise.Value.Verwaltung;

        var schachtTid = mitBauwerk ? geonis!.Bauwerk! : kennungen.Fuer("Normschacht", nummer);
        objekte.Add(new XtfNeuObjekt(
            "Normschacht", schachtTid,
            SchachtFelder(record, nummer, hinweise),
            [.. verwaltung, new XtfNeuVerweis("EigentuemerRef", eigentuemer)]));

        // Der Abwasserknoten ist der Anschlusspunkt des Schachts ans Netz.
        var knotenTid = mitGeonis ? geonis!.Knoten! : kennungen.Fuer("Abwasserknoten", nummer);
        objekte.Add(new XtfNeuObjekt(
            "Abwasserknoten", knotenTid,
            [new KeyValuePair<string, string>("Bezeichnung", Kuerze(nummer, HaltungspunktNameMax))],
            [new XtfNeuVerweis("AbwasserbauwerkRef", schachtTid), .. verwaltung]));

        knoten[nummer] = knotenTid;
        return true;
    }

    /// <summary>
    /// Die Sachfelder eines Kanals oder einer Haltung. Es gelten dieselben
    /// Umsetzungsregeln wie beim Revisionsweg — kein eigenes Vokabular, keine zweite
    /// Wahrheit. Was nicht eindeutig passt, bleibt weg statt geraten zu werden.
    /// </summary>
    private static List<KeyValuePair<string, string>> Sachfelder(
        HaltungRecord record, string name, IReadOnlyDictionary<string, string> karte,
        List<string> hinweise)
    {
        var felder = new List<KeyValuePair<string, string>>
        {
            new("Bezeichnung", name)
        };

        foreach (var (xtfName, projektFeld) in karte)
        {
            var roh = (record.GetFieldValue(projektFeld) ?? "").Trim();
            if (roh.Length == 0)
                continue;

            var wert = XtfStammdatenPlanBuilder.NachXtfWert(xtfName, roh, "SIA405_ABWASSER_2020_LV95");
            if (!string.IsNullOrEmpty(wert))
            {
                felder.Add(new(xtfName, wert));
                continue;
            }

            // Ein gesetzter, aber nicht abbildbarer Wert darf nicht still verschwinden.
            // "GFK" etwa hat in SIA405 bewusst kein Gegenstueck — es ist nicht dasselbe
            // wie Kunststoff_Polyester_GUP. Ohne diesen Hinweis fehlte das Material
            // spurlos in der Datei, obwohl es im Programm dasteht.
            hinweise.Add(NichtGeschrieben(name, xtfName, roh));
        }

        return felder;
    }

    /// <summary>
    /// Der Hinweis fuer einen gesetzten Wert, der nicht in die Datei kann. Eine zu lange
    /// Bemerkung scheitert nur an der Grenze <c>TEXT*80</c>, nicht an einem unbekannten
    /// Begriff — und genau das muss der Bericht sagen, damit jemand kuerzen kann. Der
    /// Revisionsweg unterscheidet das schon; hier fehlte es (Jagdmatt, 46 Haltungen).
    /// </summary>
    private static string NichtGeschrieben(string wofuer, string xtfName, string roh)
        => string.Equals(xtfName, "Bemerkung", StringComparison.Ordinal)
           && XtfStammdatenPlanBuilder.BemerkungZuLang(roh, out var zeichen)
            ? $"{wofuer}: die Bemerkung ist {zeichen} Zeichen lang, das Modell laesst " +
              $"{XtfStammdatenPlanBuilder.BemerkungGrenze} zu — nicht geschrieben."
            : $"{wofuer}: {xtfName} = \"{roh}\" hat in SIA405 keinen Wert — nicht geschrieben.";

    private static List<KeyValuePair<string, string>> SchachtFelder(
        SchachtRecord record, string nummer, List<string> hinweise)
    {
        var felder = new List<KeyValuePair<string, string>>
        {
            new("Bezeichnung", nummer)
        };

        foreach (var (xtfName, projektFeld) in XtfSchachtPlanBuilder.Felder)
        {
            var roh = (XtfSchachtPlanBuilder.Wert(record, projektFeld) ?? "").Trim();
            if (roh.Length == 0)
                continue;

            var wert = XtfSchachtPlanBuilder.NachXtfWert(xtfName, roh);
            if (!string.IsNullOrEmpty(wert))
            {
                felder.Add(new(xtfName, wert));
                continue;
            }

            hinweise.Add(NichtGeschrieben($"Schacht {nummer}", xtfName, roh));
        }

        var masse = XtfSchachtPlanBuilder.Masse(record, $"Schacht {nummer}", hinweise);
        if (masse is not null)
        {
            felder.Add(new("Dimension1", masse.Value.Dimension1));
            felder.Add(new("Dimension2", masse.Value.Dimension2));

            var widerspruch = XtfSchachtPlanBuilder.Formwiderspruch(record, masse);
            if (widerspruch is not null)
                hinweise.Add($"Schacht {nummer}: {widerspruch}");
        }

        return felder;
    }

    /// <summary>
    /// Vergibt eindeutige Haltungspunkt-Bezeichnungen innerhalb der Laengengrenze.
    /// Ist ein Name schon vergeben oder zu lang, wird er gekuerzt und durchnummeriert.
    /// </summary>
    private sealed class Punktnamen(int max)
    {
        private readonly HashSet<string> _vergeben = new(StringComparer.OrdinalIgnoreCase);

        public string Eindeutig(string wunsch)
        {
            var kurz = Kuerze(wunsch, max);
            if (_vergeben.Add(kurz))
                return kurz;

            for (var i = 2; i < 100_000; i++)
            {
                var zusatz = "~" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var kandidat = Kuerze(wunsch, max - zusatz.Length) + zusatz;
                if (_vergeben.Add(kandidat))
                    return kandidat;
            }

            throw new InvalidOperationException(
                $"Fuer \"{wunsch}\" konnte keine eindeutige Bezeichnung vergeben werden.");
        }
    }

    private static string Kuerze(string text, int max)
        => text.Length <= max ? text : text[..max];

    /// <summary>
    /// Vergibt die Organisationen der Datei und die drei Pflichtverweise darauf.
    ///
    /// Gesetzte Werte fuer Datenherr und Datenlieferant gewinnen. Nur ein leeres Feld
    /// faellt auf den Eigentuemer zurueck. Ein unbekannter gesetzter Name sperrt das
    /// ganze Bauteil, statt still eine andere Organisation einzutragen.
    /// </summary>
    private sealed class Organisationsbuch(XtfNeuKennungen kennungen, List<XtfNeuObjekt> objekte)
    {
        private readonly Dictionary<string, string> _jeName = new(StringComparer.OrdinalIgnoreCase);

        public (string Eigentuemer, IReadOnlyList<XtfNeuVerweis> Verwaltung)? Verweise(
            string? eigentuemer,
            string? datenherr,
            string? datenlieferant,
            string wofuer,
            List<string> hinweise)
        {
            var eigentuemerName = Pruefe(eigentuemer, "Eigentuemer", wofuer, hinweise);
            if (eigentuemerName is null)
                return null;

            var datenherrName = PruefeOderEigentuemer(
                datenherr, eigentuemerName, "Datenherrn", wofuer, hinweise);
            if (datenherrName is null)
                return null;

            var datenlieferantName = PruefeOderEigentuemer(
                datenlieferant, eigentuemerName, "Datenlieferanten", wofuer, hinweise);
            if (datenlieferantName is null)
                return null;

            // Erst nachdem alle drei Namen geprueft sind, entstehen Objekte. So bleibt
            // bei einem Fehler keine verwaiste Organisation in einer sonst gueltigen Datei.
            var eigentuemerRef = Erzeuge(eigentuemerName);
            IReadOnlyList<XtfNeuVerweis> verwaltung =
            [
                new("DatenherrRef", Erzeuge(datenherrName)),
                new("DatenlieferantRef", Erzeuge(datenlieferantName))
            ];

            return (eigentuemerRef, verwaltung);
        }

        private static string? Pruefe(
            string? wert, string rolle, string wofuer, List<string> hinweise)
        {
            var roh = (wert ?? "").Trim();
            if (roh.Length == 0)
            {
                hinweise.Add(
                    $"{wofuer}: ohne Eigentuemer nicht geschrieben — in SIA405 ist der " +
                    "Verweis auf eine Organisation Pflicht.");
                return null;
            }

            var name = EigentumVokabular.Normalisieren(roh);
            if (EigentumVokabular.NachOrganisationstyp(name) is null)
            {
                hinweise.Add(
                    $"{wofuer}: fuer den {rolle} \"{name}\" ist kein Organisationstyp " +
                    "nach SIA405 bekannt — nicht geschrieben.");
                return null;
            }

            return name;
        }

        private static string? PruefeOderEigentuemer(
            string? wert,
            string eigentuemer,
            string rolle,
            string wofuer,
            List<string> hinweise)
        {
            if (string.IsNullOrWhiteSpace(wert))
                return eigentuemer;

            return Pruefe(wert, rolle, wofuer, hinweise);
        }

        private string Erzeuge(string name)
        {
            if (_jeName.TryGetValue(name, out var bekannt))
                return bekannt;

            var typ = EigentumVokabular.NachOrganisationstyp(name)!;
            var tid = kennungen.Fuer("Organisation", name);
            objekte.Add(new XtfNeuObjekt(
                "Organisation", tid,
                // "Status" ist im Basismodell MANDATORY (aktiv | untergegangen). Eine
                // neu angelegte Organisation ist aktiv; ohne das Feld weist der
                // ilivalidator die ganze Datei ab ("Attribute Status requires a value").
                [new KeyValuePair<string, string>("Bezeichnung", name),
                 new KeyValuePair<string, string>("Organisationstyp", typ),
                 new KeyValuePair<string, string>("Status", "aktiv")],
                [],
                ImTopicAdministration: true));

            _jeName[name] = tid;
            return tid;
        }
    }

    /// <summary>
    /// Das Hoehen-Breiten-Verhaeltnis der Haltung fuer ihr Rohrprofil, oder <c>null</c>.
    ///
    /// Die Haltung kennt in SIA405 nur die lichte Hoehe; die Breite steckt als Verhaeltnis
    /// am Rohrprofil. Rund (Breite leer oder gleich der Hoehe) ergibt keines. Zwei
    /// verschiedene Masse ohne Profiltyp oder mit Kreisprofil widersprechen sich; dann
    /// wird das gemeldet und nichts geraten.
    /// </summary>
    private static string? Verhaeltnis(HaltungRecord record, string name, List<string> hinweise)
    {
        var hoehe = record.GetFieldValue(FieldKeys.NominalDiameterMm);
        var breite = record.GetFieldValue(FieldKeys.ClearWidthMm);
        var verhaeltnis = XtfRohrprofilVerhaeltnis.Berechne(hoehe, breite);
        var profiltyp = (record.GetFieldValue(FieldKeys.ProfileType) ?? "").Trim();
        var istKreis = string.Equals(ProfiltypVokabular.NachNorm(profiltyp), "Kreisprofil", StringComparison.Ordinal);

        // Rund (Breite leer oder gleich) ist am Kreisprofil das Verhaeltnis 1. GEONIS
        // fuehrt neben der Hoehe die Breite und rechnet sie daraus; ohne den Wert bliebe
        // sie dort leer (Wunsch Trigonet 2026-09-04).
        if (verhaeltnis is null)
            return istKreis ? XtfRohrprofilVerhaeltnis.Rund : null;

        if (profiltyp.Length == 0)
        {
            hinweise.Add(
                $"{name}: Hoehe {hoehe} und Breite {breite}, aber kein Profiltyp — das " +
                "Hoehen-Breiten-Verhaeltnis wird ohne Profil nicht geschrieben.");
            return null;
        }

        if (istKreis)
        {
            hinweise.Add(
                $"{name}: Kreisprofil mit zwei verschiedenen Massen ({hoehe} x {breite}) — " +
                "das Hoehen-Breiten-Verhaeltnis wird nicht geschrieben.");
            return null;
        }

        return verhaeltnis;
    }

    /// <summary>
    /// Ein Rohrprofil je Profiltyp und Hoehen-Breiten-Verhaeltnis, von allen passenden
    /// Haltungen geteilt. Der Kataster legt je Haltung ein eigenes an; das ist erlaubt
    /// ({0..*}), blaeht die Datei aber ohne Nutzen auf.
    /// </summary>
    private sealed class Profilbuch(XtfNeuKennungen kennungen, List<XtfNeuObjekt> objekte)
    {
        private readonly Dictionary<string, string> _jeSchluessel = new(StringComparer.OrdinalIgnoreCase);

        public string? Verweis(
            string? profiltyp,
            string? verhaeltnis,
            IReadOnlyList<XtfNeuVerweis> verwaltung,
            GeonisKennungen? geonis,
            string wofuer,
            List<string> hinweise)
        {
            var roh = (profiltyp ?? "").Trim();
            if (roh.Length == 0)
                return null;

            var norm = ProfiltypVokabular.NachNorm(roh);
            if (norm is null)
                return null;

            // Ein Rohrprofil wird in GEONIS von vielen Haltungen geteilt. Seine Kennung
            // darf der Export nur wiederverwenden, wenn es wirklich dasselbe Profil
            // ist — sonst schriebe ein Import den Typ ALLER Haltungen an diesem Profil
            // um. Bei Abweichung bekommt die Haltung ein eigenes Profil, und der
            // Bericht nennt den Unterschied.
            if (geonis is not null && SiaObjektkennung.IstGueltig(geonis.Rohrprofil))
            {
                var katasterTyp = (geonis.RohrprofilTyp ?? "").Trim();
                var rund = verhaeltnis is null || verhaeltnis == XtfRohrprofilVerhaeltnis.Rund;
                if (rund && string.Equals(katasterTyp, norm, StringComparison.Ordinal))
                    return Kataster(geonis.Rohrprofil!, norm, verhaeltnis, verwaltung);

                hinweise.Add(
                    $"{wofuer}: Rohrprofil weicht vom Kataster ab (Kataster: " +
                    $"{(katasterTyp.Length == 0 ? "unbekannt" : katasterTyp)}, Projekt: {norm}" +
                    (verhaeltnis is null ? "" : $" {verhaeltnis}") +
                    ") — die Haltung bekommt ein eigenes Profil.");
            }

            var schluessel = verhaeltnis is null ? norm : $"{norm}|{verhaeltnis}";
            if (_jeSchluessel.TryGetValue(schluessel, out var bekannt))
                return bekannt;

            // Die Bezeichnung ist mit dem Datenherrn zusammen UNIQUE: Zwei Profile
            // desselben Typs mit verschiedenem Verhaeltnis brauchen verschiedene Namen.
            // Das runde Verhaeltnis 1 gehoert zum Kreisprofil und braucht keinen Zusatz.
            var bezeichnung = verhaeltnis is null || verhaeltnis == XtfRohrprofilVerhaeltnis.Rund
                ? norm
                : $"{norm} {verhaeltnis}";
            var felder = new List<KeyValuePair<string, string>>
            {
                new("Bezeichnung", Kuerze(bezeichnung, HaltungspunktNameMax))
            };
            if (verhaeltnis is not null)
                felder.Add(new(XtfRohrprofilVerhaeltnis.Attribut, verhaeltnis));
            felder.Add(new("Profiltyp", norm));

            var tid = kennungen.Fuer("Rohrprofil", schluessel);
            objekte.Add(new XtfNeuObjekt("Rohrprofil", tid, felder, verwaltung));

            _jeSchluessel[schluessel] = tid;
            return tid;
        }

        /// <summary>Das GEONIS-Profil unter seiner eigenen Kennung, je Kennung genau einmal.</summary>
        private string Kataster(string tid, string norm, string? verhaeltnis, IReadOnlyList<XtfNeuVerweis> verwaltung)
        {
            var schluessel = "kataster|" + tid;
            if (_jeSchluessel.TryGetValue(schluessel, out var bekannt))
                return bekannt;

            var felder = new List<KeyValuePair<string, string>>
            {
                new("Bezeichnung", Kuerze(norm, HaltungspunktNameMax))
            };
            if (verhaeltnis is not null)
                felder.Add(new(XtfRohrprofilVerhaeltnis.Attribut, verhaeltnis));
            felder.Add(new("Profiltyp", norm));

            objekte.Add(new XtfNeuObjekt("Rohrprofil", tid, felder, verwaltung));

            _jeSchluessel[schluessel] = tid;
            return tid;
        }
    }
}
