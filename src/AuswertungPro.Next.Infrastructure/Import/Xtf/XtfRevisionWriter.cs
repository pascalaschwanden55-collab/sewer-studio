using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

public sealed record XtfRevisionErgebnis(
    bool Ok,
    string? Zielpfad,
    string? Fehler,
    int Geaendert,
    int Neu,
    int Entfernt);

/// <summary>
/// Schreibt die revidierte XTF — ausschliesslich nach dem uebergebenen Plan.
///
/// Der Ausfuehrer trifft keine eigenen Entscheidungen mehr (gleiches Muster wie beim
/// plan-gesteuerten YOLO-Export). Er laedt das Original, aendert genau die im Plan
/// genannten Stellen und laesst alles Uebrige unangetastet stehen: Geometrie,
/// Dateiverweise, Datentraeger, unbekannte Elemente.
///
/// Feste Grenzen:
/// - Das Original wird nur gelesen und nie veraendert.
/// - Eine vorhandene Zieldatei wird nie ueberschrieben.
/// - Ein Plan mit offenen Entscheidungen wird nicht geschrieben.
/// </summary>
public static class XtfRevisionWriter
{
    public static XtfRevisionErgebnis Schreibe(
        string originalPfad,
        XtfRevisionPlan plan,
        string zielPfad,
        DateOnly? aenderungsdatum = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var pruefung = Pruefe(originalPfad, plan, zielPfad);
        if (pruefung is not null)
            return pruefung;

        var doc = XDocument.Load(originalPfad);
        var stand = (aenderungsdatum ?? DateOnly.FromDateTime(DateTime.Now)).ToString("yyyyMMdd");

        // Alle Objekte mit Kennung, nicht nur Kanalschaeden: Eine Aenderung kann auch ein
        // Stammdaten-Objekt der SIA405-XTF betreffen (Klasse "Kanal").
        var elementeJeTid = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var node in doc.Descendants())
        {
            var tid = (string?)node.Attribute("TID");
            if (!string.IsNullOrWhiteSpace(tid) && !elementeJeTid.ContainsKey(tid!))
                elementeJeTid[tid!] = node;
        }

        var vergebeneTids = new HashSet<string>(
            doc.Descendants().Select(e => (string?)e.Attribute("TID")).Where(t => !string.IsNullOrWhiteSpace(t))!,
            StringComparer.Ordinal);

        int geaendert = 0, neu = 0, entfernt = 0;

        // Zuerst die Organisationen: Eine Position darf erst dann auf sie verweisen,
        // wenn es das Objekt in der Datei wirklich gibt.
        var organisationen = 0;
        foreach (var organisation in plan.Organisationen)
        {
            if (!ErzeugeOrganisation(doc, organisation, vergebeneTids, stand))
                break;

            organisationen++;
        }

        if (organisationen != plan.Organisationen.Count)
        {
            return Fehler(
                "Eine benoetigte Organisation konnte nicht angelegt werden — es wurde nichts " +
                $"geschrieben. Geplant: {plan.Organisationen.Count}, angelegt: {organisationen}.");
        }

        foreach (var position in plan.Positionen)
        {
            switch (position.Art)
            {
                case XtfRevisionAenderung.Geaendert:
                    if (position.KanalschadenTid is not null
                        && elementeJeTid.TryGetValue(position.KanalschadenTid, out var zuAendern))
                    {
                        foreach (var feld in position.Felder)
                            SetzeFeld(zuAendern, feld);
                        // Nur nachfuehren, wo die Datei dieses Feld selbst fuehrt. In der
                        // SIA405-XTF gehoert "Letzte_Aenderung" in die Struktur "Metaattribute"
                        // und nicht direkt an den Kanal — es wird dort deshalb nicht erfunden.
                        AktualisiereVorhandenesKind(zuAendern, "Letzte_Aenderung", stand);
                        geaendert++;
                    }
                    break;

                case XtfRevisionAenderung.Entfernt:
                    if (position.KanalschadenTid is not null
                        && elementeJeTid.TryGetValue(position.KanalschadenTid, out var zuEntfernen))
                    {
                        zuEntfernen.Remove();
                        elementeJeTid.Remove(position.KanalschadenTid);
                        entfernt++;
                    }
                    break;

                case XtfRevisionAenderung.Neu:
                    if (ErzeugeNeuen(doc, position, vergebeneTids, stand))
                        neu++;
                    break;
            }
        }

        // Fail-closed: Was der Plan verlangt, muss auch angewandt worden sein. Sonst entstuende
        // eine Datei, die wie eine Revision aussieht und keine ist.
        if (geaendert != plan.AnzahlGeaendert || neu != plan.AnzahlNeu || entfernt != plan.AnzahlEntfernt)
        {
            return Fehler(
                "Der Plan konnte nicht vollstaendig angewandt werden — es wurde nichts geschrieben. " +
                $"Geplant: {plan.AnzahlGeaendert} geaendert, {plan.AnzahlNeu} neu, {plan.AnzahlEntfernt} entfernt. " +
                $"Angewandt: {geaendert} geaendert, {neu} neu, {entfernt} entfernt.");
        }

        return Speichere(doc, zielPfad, geaendert, neu, entfernt);
    }

    private static XtfRevisionErgebnis? Pruefe(string originalPfad, XtfRevisionPlan plan, string zielPfad)
    {
        if (string.IsNullOrWhiteSpace(originalPfad) || !File.Exists(originalPfad))
            return Fehler($"Die Originaldatei wurde nicht gefunden: {originalPfad}");

        if (string.IsNullOrWhiteSpace(zielPfad))
            return Fehler("Es wurde kein Zielpfad angegeben.");

        if (string.Equals(Path.GetFullPath(originalPfad), Path.GetFullPath(zielPfad), StringComparison.OrdinalIgnoreCase))
            return Fehler("Die Revision darf das Original nicht ueberschreiben.");

        if (File.Exists(zielPfad))
            return Fehler($"Die Zieldatei besteht bereits und wird nicht ueberschrieben: {zielPfad}");

        if (plan.BrauchtEntscheidung)
        {
            return Fehler(
                "Der Plan enthaelt offene Faelle, die nicht eindeutig zugeordnet werden konnten. " +
                "Solange sie offen sind, wird nichts geschrieben.");
        }

        return null;
    }

    /// <summary>
    /// Legt einen neuen Kanalschaden neben den letzten der gleichen Untersuchung.
    /// Ohne bestehende Untersuchung im Original wird nichts erfunden.
    /// </summary>
    private static bool ErzeugeNeuen(
        XDocument doc,
        XtfRevisionPosition position,
        HashSet<string> vergebeneTids,
        string stand)
    {
        var geschwister = doc.Descendants()
            .Where(e => e.Name.LocalName.EndsWith("Kanalschaden", StringComparison.OrdinalIgnoreCase))
            .LastOrDefault(e => string.Equals(
                (string?)e.Elements()
                    .FirstOrDefault(k => string.Equals(k.Name.LocalName, "UntersuchungRef", StringComparison.Ordinal))
                    ?.Attribute("REF"),
                position.UntersuchungTid,
                StringComparison.Ordinal));

        if (geschwister is null)
            return false;

        var tid = NaechsteTid(vergebeneTids);
        var neu = new XElement(geschwister.Name, new XAttribute("TID", tid));
        neu.Add(new XElement(geschwister.Name.Namespace + "Letzte_Aenderung", stand));
        neu.Add(new XElement(
            geschwister.Name.Namespace + "UntersuchungRef",
            new XAttribute("REF", position.UntersuchungTid)));

        foreach (var feld in position.Felder)
        {
            if (!string.IsNullOrWhiteSpace(feld.Neu))
                neu.Add(new XElement(geschwister.Name.Namespace + feld.Name, feld.Neu));
        }

        geschwister.AddAfterSelf(neu);
        return true;
    }

    /// <summary>
    /// Eine neue Kennung im Stil der Datei. INTERLIS verlangt nur Eindeutigkeit innerhalb
    /// der Uebertragung; geprueft wird gegen alle bereits vergebenen Kennungen.
    /// </summary>
    private static string NaechsteTid(HashSet<string> vergeben)
    {
        var vorlage = vergeben.FirstOrDefault(t => t.StartsWith("ch", StringComparison.OrdinalIgnoreCase));
        var praefix = vorlage is { Length: >= 8 } ? vorlage[..8] : "chREV000";

        for (var i = 1; i < 1_000_000; i++)
        {
            var kandidat = $"{praefix}R{i:D6}";
            if (vergeben.Add(kandidat))
                return kandidat;
        }

        throw new InvalidOperationException("Es konnte keine freie Kennung vergeben werden.");
    }

    /// <summary>
    /// Feldreihenfolge der SIA405-Klasse "Kanal" — erst die geerbten Felder der Oberklassen,
    /// dann die eigenen, jeweils so wie im Modell (SIA405_Abwasser, Klasse Abwasserbauwerk
    /// bzw. Kanal). INTERLIS gibt diese Reihenfolge vor; ein neu eingefuegtes Feld darf
    /// deshalb nicht einfach hinten angehaengt werden.
    /// </summary>
    private static readonly string[] KanalFeldreihenfolge =
    [
        "OBJ_ID", "Metaattribute",
        "Akten", "Baujahr", "BaulicherZustand", "Baulos", "Bemerkung", "Bezeichnung",
        "Bruttokosten", "Detailgeometrie", "Ersatzjahr", "Finanzierung", "Inspektionsintervall",
        "Sanierungsbedarf", "Standortname", "Status", "Subventionen", "WBW_Basisjahr",
        "WBW_Bauart", "Wiederbeschaffungswert", "Zugaenglichkeit",
        "Bettung_Umhuellung", "FunktionHierarchisch", "FunktionHydraulisch",
        "Nutzungsart_geplant", "Nutzungsart_Ist", "Rohrlaenge", "Spuelintervall", "Verbindungsart"
    ];

    /// <summary>
    /// Feldreihenfolge der SIA405-Klasse "Normschacht" — derselbe geerbte Block von
    /// "Abwasserbauwerk", danach die eigenen Felder des Schachts.
    ///
    /// Nur ein Rueckfall: Die drei gepruefeten Lieferungen ordnen den Normschacht anders
    /// (Kantonsexport: Bezeichnung, Funktion, Material, Dimension1, Dimension2, Status,
    /// Sanierungsbedarf). Massgebend bleibt deshalb auch hier zuerst ein Geschwister-Objekt
    /// derselben Datei; diese Liste greift erst, wenn keines das Feld fuehrt.
    /// </summary>
    private static readonly string[] NormschachtFeldreihenfolge =
    [
        "OBJ_ID", "Metaattribute",
        "Akten", "Baujahr", "BaulicherZustand", "Baulos", "Bemerkung", "Bezeichnung",
        "Bruttokosten", "Detailgeometrie", "Ersatzjahr", "Finanzierung", "Inspektionsintervall",
        "Sanierungsbedarf", "Standortname", "Status", "Subventionen", "WBW_Basisjahr",
        "WBW_Bauart", "Wiederbeschaffungswert", "Zugaenglichkeit",
        "Amphibienausstieg", "Dimension1", "Dimension2", "Funktion",
        "Interventionsmoeglichkeit", "Material", "Oberflaechenzulauf"
    ];

    /// <summary>Die Modellreihenfolge, die zur Klasse des Objekts passt.</summary>
    /// <summary>
    /// Feldreihenfolge der SIA405-Klasse "Rohrprofil" (VSA_BaseClass, dann die eigenen
    /// Felder). Das Hoehen-Breiten-Verhaeltnis steht im Modell VOR dem Profiltyp; im
    /// Kantonsexport fuehrt kein einziges Rohrprofil das Feld, ein Geschwister-Vorbild
    /// gibt es dort also nicht.
    /// </summary>
    private static readonly string[] RohrprofilFeldreihenfolge =
    [
        "OBJ_ID", "Metaattribute",
        "Bemerkung", "Bezeichnung", "HoehenBreitenverhaeltnis", "Profiltyp"
    ];

    private static string[] Feldreihenfolge(XElement parent)
    {
        var name = parent.Name.LocalName;
        if (name.EndsWith(".Normschacht", StringComparison.Ordinal))
            return NormschachtFeldreihenfolge;
        if (name.EndsWith(".Rohrprofil", StringComparison.Ordinal))
            return RohrprofilFeldreihenfolge;
        return KanalFeldreihenfolge;
    }

    /// <summary>
    /// Legt eine fehlende Organisation im Topic <c>Administration</c> an.
    ///
    /// Vorbild ist eine bereits vorhandene Organisation derselben Datei: Von ihr kommen
    /// Elementname, Namensraum und die Stelle im Dokument. Ohne Vorbild wird nichts
    /// erfunden — dann fehlt das Topic, und der Aufbau der Kundendatei bliebe geraten.
    /// Eine schon vergebene Kennung ist ebenfalls ein harter Abbruch.
    /// </summary>
    private static bool ErzeugeOrganisation(
        XDocument doc,
        XtfNeueOrganisation organisation,
        HashSet<string> vergebeneTids,
        string stand)
    {
        if (!vergebeneTids.Add(organisation.Tid))
            return false;

        var vorbild = doc.Descendants()
            .LastOrDefault(e => e.Name.LocalName.EndsWith(".Organisation", StringComparison.Ordinal));

        if (vorbild is null)
            return false;

        var neu = new XElement(vorbild.Name, new XAttribute("TID", organisation.Tid));

        // "Letzte_Aenderung" nur, wo die Datei das Feld selbst fuehrt — sonst waere es
        // ein erfundenes Feld, wie schon beim Kanal.
        if (FindeKind(vorbild, "Letzte_Aenderung") is not null)
            neu.Add(new XElement(vorbild.Name.Namespace + "Letzte_Aenderung", stand));

        neu.Add(new XElement(vorbild.Name.Namespace + "Bezeichnung", organisation.Bezeichnung));
        neu.Add(new XElement(vorbild.Name.Namespace + "Organisationstyp", organisation.Organisationstyp));
        neu.Add(new XElement(vorbild.Name.Namespace + "Status", "aktiv"));

        vorbild.AddAfterSelf(neu);
        return true;
    }

    /// <summary>
    /// Schreibt ein Feld — als Text oder, bei einem Verweis, ins Attribut <c>REF</c>.
    /// </summary>
    private static void SetzeFeld(XElement parent, XtfRevisionFeld feld)
    {
        if (!feld.IstVerweis)
        {
            SetzeKind(parent, feld.Name, feld.Neu);
            return;
        }

        if (string.IsNullOrWhiteSpace(feld.Neu))
            return;

        var kind = FindeKind(parent, feld.Name);
        if (kind is not null)
        {
            kind.SetAttributeValue("REF", feld.Neu);
            return;
        }

        var neu = new XElement(
            parent.Name.Namespace + SchreibweiseAusDatei(parent, feld.Name),
            new XAttribute("REF", feld.Neu!));

        var nachfolger = ErstesFeldDanach(parent, feld.Name);
        if (nachfolger is null)
            parent.Add(neu);
        else
            nachfolger.AddBeforeSelf(neu);
    }

    private static void SetzeKind(XElement parent, string name, string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return;

        var kind = FindeKind(parent, name);
        if (kind is not null)
        {
            kind.Value = wert;
            return;
        }

        // Ein neues Feld bekommt die Schreibweise, die die Datei fuer dieses Feld
        // verwendet — nicht die des Modells. Ohne Vorbild bleibt es beim Modellnamen.
        var neu = new XElement(parent.Name.Namespace + SchreibweiseAusDatei(parent, name), wert);
        var nachfolger = ErstesFeldDanach(parent, name);
        if (nachfolger is null)
            parent.Add(neu);
        else
            nachfolger.AddBeforeSelf(neu);
    }

    /// <summary>
    /// Das erste vorhandene Feld, hinter dem <paramref name="name"/> nicht mehr stehen darf.
    /// <c>null</c> heisst: ans Ende haengen.
    ///
    /// Zuerst wird die Datei selbst befragt. Ein Geschwister-Objekt derselben Klasse, das
    /// das Feld bereits fuehrt, zeigt genau, wohin es gehoert — und zwar unabhaengig davon,
    /// welchem Modellableger die Datei folgt. Das ist noetig, weil die Reihenfolge zwischen
    /// Dateien wirklich verschieden ist: Gemessen an drei echten Lieferungen setzt der
    /// GEP-Export Zone 1.15 <c>AbwasserbauwerkRef</c> direkt hinter die Bezeichnung, der
    /// Kantonsexport ganz ans Ende. Innerhalb einer Datei ist sie dagegen konsistent.
    ///
    /// Erst wenn kein Geschwister das Feld fuehrt, gilt die Modellreihenfolge der Klasse
    /// "Kanal". Kennt auch die den Namen nicht, wird nichts geraten und hinten angehaengt.
    /// </summary>
    private static XElement? ErstesFeldDanach(XElement parent, string name)
    {
        var ausDerDatei = NachfolgerAusGeschwister(parent, name);
        if (ausDerDatei is not null)
            return ausDerDatei;

        var reihenfolge = Feldreihenfolge(parent);
        var platz = Array.IndexOf(reihenfolge, name);
        if (platz < 0)
            return ErsterVerweis(parent, name);

        foreach (var kind in parent.Elements())
        {
            var stelle = Array.IndexOf(reihenfolge, kind.Name.LocalName);
            if (stelle > platz)
                return kind;
        }

        return ErsterVerweis(parent, name);
    }

    /// <summary>
    /// Das erste Verweis-Element des Objekts — der Punkt, vor dem ein neues Attribut
    /// noch stehen darf. Nur ein Rueckfall, wenn weder ein Geschwister-Objekt noch die
    /// Modellliste eine Stelle kennen.
    ///
    /// ACHTUNG, hier weicht die Kundendatei von der Norm ab, und diese Regel folgt der
    /// Datei. Das INTERLIS-Referenzhandbuch (eCH-0031 V2.1.0, Kapitel 4.3) schreibt das
    /// "Zwiebelprinzip" vor: je Vererbungsstufe zuerst die Rollen, dann die Attribute,
    /// dann die eingebetteten Beziehungen — DANN erst die naechste Stufe. Danach
    /// gehoerten <c>DatenherrRef</c> (an <c>VSA_BaseClass</c>) und <c>EigentuemerRef</c>
    /// (an <c>Abwasserbauwerk</c>) VOR die Attribute des <c>Kanal</c> wie
    /// <c>Verbindungsart</c>.
    ///
    /// Der Kantonsexport von Abwasser Uri haelt sich nicht daran: Dort stehen alle
    /// Verweise am Ende, hinter allen Attributen. Eine Revision dieser Datei bleibt
    /// deshalb besser bei deren eigener Ordnung, statt mitten im Bestand eine zweite
    /// einzufuehren — das Ergebnis waere innerhalb derselben Datei uneinheitlich.
    ///
    /// Ein Verweis selbst wird nie vorgezogen; er gehoert zu den Verweisen.
    /// </summary>
    private static XElement? ErsterVerweis(XElement parent, string name)
        => name.EndsWith("Ref", StringComparison.Ordinal)
            ? null
            : parent.Elements().FirstOrDefault(e => e.Attribute("REF") is not null);

    /// <summary>
    /// Sucht ein Objekt derselben Klasse, das <paramref name="name"/> bereits fuehrt, und
    /// liefert daraus das erste nachfolgende Feld, das es auch in <paramref name="parent"/>
    /// gibt. Ohne Vorbild <c>null</c>.
    /// </summary>
    private static XElement? NachfolgerAusGeschwister(XElement parent, string name)
    {
        var vorbild = parent.Parent?
            .Elements(parent.Name)
            .FirstOrDefault(e => !ReferenceEquals(e, parent)
                                 && e.Elements().Any(k => string.Equals(k.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)));

        if (vorbild is null)
            return null;

        var namen = vorbild.Elements().Select(e => e.Name.LocalName).ToList();
        var platz = namen.FindIndex(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        if (platz < 0)
            return null;

        for (var i = platz + 1; i < namen.Count; i++)
        {
            var treffer = parent.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, namen[i], StringComparison.OrdinalIgnoreCase));
            if (treffer is not null)
                return treffer;
        }

        return null;
    }

    private static void AktualisiereVorhandenesKind(XElement parent, string name, string wert)
    {
        var kind = FindeKind(parent, name);
        if (kind is not null)
            kind.Value = wert;
    }

    /// <summary>
    /// Das vorhandene Feld <paramref name="name"/>, zuerst zeichengenau und danach ohne
    /// Ruecksicht auf Gross- und Kleinschreibung.
    ///
    /// Die zweite Runde ist noetig, weil echte Lieferungen sich darin unterscheiden: Der
    /// GEP-Export Zone 1.15 schreibt "BaulicherZustand" wie das Modell, Zone 1.17 dagegen
    /// "Baulicherzustand" mit kleinem z — an 446 Kanal- und 295 Normschacht-Objekten.
    /// Ohne diese Runde entstuende dort ein zweites Feld neben dem vorhandenen, und das
    /// Objekt traege denselben Wert zweimal in verschiedener Schreibweise.
    ///
    /// Zwei Felder, die sich nur in der Gross-/Kleinschreibung unterscheiden, kennt
    /// INTERLIS nicht; die Runde kann deshalb nichts Falsches treffen.
    /// </summary>
    private static XElement? FindeKind(XElement parent, string name)
        => parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.Ordinal))
           ?? parent.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Die Schreibweise, die diese Datei fuer <paramref name="name"/> verwendet — abgelesen
    /// an einem Objekt derselben Klasse, das das Feld bereits fuehrt. Ohne Vorbild bleibt
    /// es beim uebergebenen Namen.
    /// </summary>
    private static string SchreibweiseAusDatei(XElement parent, string name)
    {
        var vorbild = parent.Parent?
            .Elements(parent.Name)
            .SelectMany(e => e.Elements())
            .FirstOrDefault(k => string.Equals(k.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

        return vorbild?.Name.LocalName ?? name;
    }

    /// <summary>
    /// Zuerst in eine Nebendatei schreiben, dann umbenennen: Ein Abbruch mittendrin
    /// hinterlaesst nie eine halbe Revision unter dem Zielnamen.
    /// </summary>
    private static XtfRevisionErgebnis Speichere(
        XDocument doc,
        string zielPfad,
        int geaendert,
        int neu,
        int entfernt)
    {
        var voll = Path.GetFullPath(zielPfad);
        var ordner = Path.GetDirectoryName(voll);
        if (string.IsNullOrWhiteSpace(ordner))
            return Fehler($"Ungueltiger Zielpfad: {zielPfad}");

        Directory.CreateDirectory(ordner);
        var temp = Path.Combine(ordner, $".{Path.GetFileName(voll)}.{Guid.NewGuid():N}.tmp");

        try
        {
            doc.Save(temp);
            File.Move(temp, voll, overwrite: false);
            return new XtfRevisionErgebnis(true, voll, null, geaendert, neu, entfernt);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* Aufraeumen ist Nebensache */ }
            return Fehler($"Die Revision konnte nicht geschrieben werden: {ex.Message}");
        }
    }

    private static XtfRevisionErgebnis Fehler(string text)
        => new(false, null, text, 0, 0, 0);
}
