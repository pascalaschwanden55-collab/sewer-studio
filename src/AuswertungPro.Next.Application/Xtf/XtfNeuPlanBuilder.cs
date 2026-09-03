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
/// vorhandene Kundendatei ergaenzt, hier entsteht eine Datei fuer Objekte, die es im
/// Kataster noch gar nicht gibt — typischerweise private Anschlussleitungen.
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
            {
                geschriebeneHaltungen++;
            }
        }

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

        var eigentuemer = organisationen.Verweis(record.GetFieldValue(FieldKeys.Owner), name, hinweise);
        if (eigentuemer is null)
            return false;

        var verwaltung = organisationen.Verwaltung(eigentuemer);
        var profilTid = profile.Verweis(record.GetFieldValue(FieldKeys.ProfileType), verwaltung);

        var geometrie = geometrien is not null && geometrien.TryGetValue(name, out var g) ? g : null;
        if (geometrie is null)
            hinweise.Add($"{name}: kein Verlauf gefunden — die Haltung geht ohne Geometrie hinaus.");

        var (vonTid, nachTid) = BaueHaltungspunkte(
            record, name, kennungen, verwaltung, objekte, knoten, geometrie, punktnamen, hinweise);

        var kanalTid = kennungen.Fuer("Kanal", name);
        objekte.Add(new XtfNeuObjekt(
            "Kanal", kanalTid,
            Sachfelder(record, name, XtfStammdatenPlanBuilder.Felder, hinweise),
            [.. verwaltung, new XtfNeuVerweis("EigentuemerRef", eigentuemer)]));

        var haltungTid = kennungen.Fuer("Haltung", name);
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
        List<string> hinweise)
    {
        var vonTid = EinHaltungspunkt(
            name, "von", (record.GetFieldValue(SchachtObenFeld) ?? "").Trim(),
            kennungen, verwaltung, objekte, knoten,
            verlauf?.Punkte.FirstOrDefault(), punktnamen, hinweise);

        var nachTid = EinHaltungspunkt(
            name, "nach", (record.GetFieldValue(SchachtUntenFeld) ?? "").Trim(),
            kennungen, verwaltung, objekte, knoten,
            verlauf?.Punkte.LastOrDefault(), punktnamen, hinweise);

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
        List<string> hinweise)
    {
        var bezeichnung = punktnamen.Eindeutig($"{haltung}_{seite}");
        var tid = kennungen.Fuer("Haltungspunkt", $"{haltung}|{seite}");
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

        var eigentuemer = organisationen.Verweis(
            XtfSchachtPlanBuilder.Wert(record, FieldKeys.Owner), $"Schacht {nummer}", hinweise);
        if (eigentuemer is null)
            return false;

        var verwaltung = organisationen.Verwaltung(eigentuemer);

        var schachtTid = kennungen.Fuer("Normschacht", nummer);
        objekte.Add(new XtfNeuObjekt(
            "Normschacht", schachtTid,
            SchachtFelder(record, nummer, hinweise),
            [.. verwaltung, new XtfNeuVerweis("EigentuemerRef", eigentuemer)]));

        // Der Abwasserknoten ist der Anschlusspunkt des Schachts ans Netz.
        var knotenTid = kennungen.Fuer("Abwasserknoten", nummer);
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
            hinweise.Add(
                $"{name}: {xtfName} = \"{roh}\" hat in SIA405 keinen Wert — " +
                "nicht geschrieben.");
        }

        return felder;
    }

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

            hinweise.Add(
                $"Schacht {nummer}: {xtfName} = \"{roh}\" hat in SIA405 keinen Wert — " +
                "nicht geschrieben.");
        }

        var masse = XtfSchachtPlanBuilder.Abmessungen(
            XtfSchachtPlanBuilder.Wert(record, XtfSchachtPlanBuilder.Dimensionsfeld));
        if (masse is not null)
        {
            felder.Add(new("Dimension1", masse.Value.Dimension1));
            felder.Add(new("Dimension2", masse.Value.Dimension2));
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
    /// Datenherr und Datenlieferant zeigen auf denselben Eintrag wie der Eigentuemer.
    /// Das ist eine bewusste Standardannahme fuer eine Ersterfassung: Wer die Leitung
    /// besitzt, hat sie hier auch erfassen lassen. Im Kataster setzt die fuehrende
    /// Stelle den Datenherrn ohnehin selbst.
    /// </summary>
    private sealed class Organisationsbuch(XtfNeuKennungen kennungen, List<XtfNeuObjekt> objekte)
    {
        private readonly Dictionary<string, string> _jeName = new(StringComparer.OrdinalIgnoreCase);

        public string? Verweis(string? eigentuemer, string wofuer, List<string> hinweise)
        {
            var roh = (eigentuemer ?? "").Trim();
            if (roh.Length == 0)
            {
                hinweise.Add(
                    $"{wofuer}: ohne Eigentuemer nicht geschrieben — in SIA405 ist der " +
                    "Verweis auf eine Organisation Pflicht.");
                return null;
            }

            var name = EigentumVokabular.Normalisieren(roh);
            if (_jeName.TryGetValue(name, out var bekannt))
                return bekannt;

            var typ = EigentumVokabular.NachOrganisationstyp(name);
            if (typ is null)
            {
                hinweise.Add(
                    $"{wofuer}: fuer den Eigentuemer \"{name}\" ist kein Organisationstyp " +
                    "nach SIA405 bekannt — nicht geschrieben.");
                return null;
            }

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

        public IReadOnlyList<XtfNeuVerweis> Verwaltung(string organisation)
            => [new("DatenherrRef", organisation), new("DatenlieferantRef", organisation)];
    }

    /// <summary>
    /// Ein Rohrprofil je Profiltyp, von allen passenden Haltungen geteilt. Der Kataster
    /// legt je Haltung ein eigenes an; das ist erlaubt ({0..*}), blaeht die Datei aber
    /// ohne Nutzen auf.
    /// </summary>
    private sealed class Profilbuch(XtfNeuKennungen kennungen, List<XtfNeuObjekt> objekte)
    {
        private readonly Dictionary<string, string> _jeTyp = new(StringComparer.OrdinalIgnoreCase);

        public string? Verweis(string? profiltyp, IReadOnlyList<XtfNeuVerweis> verwaltung)
        {
            var roh = (profiltyp ?? "").Trim();
            if (roh.Length == 0)
                return null;

            var norm = SiaKanalVokabular.Profiltyp.NachNorm(roh);
            if (norm is null)
                return null;

            if (_jeTyp.TryGetValue(norm, out var bekannt))
                return bekannt;

            var tid = kennungen.Fuer("Rohrprofil", norm);
            objekte.Add(new XtfNeuObjekt(
                "Rohrprofil", tid,
                [new KeyValuePair<string, string>("Bezeichnung", Kuerze(norm, HaltungspunktNameMax)),
                 new KeyValuePair<string, string>("Profiltyp", norm)],
                verwaltung));

            _jeTyp[norm] = tid;
            return tid;
        }
    }
}
