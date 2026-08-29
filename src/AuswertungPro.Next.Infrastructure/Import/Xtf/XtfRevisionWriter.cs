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

        foreach (var position in plan.Positionen)
        {
            switch (position.Art)
            {
                case XtfRevisionAenderung.Geaendert:
                    if (position.KanalschadenTid is not null
                        && elementeJeTid.TryGetValue(position.KanalschadenTid, out var zuAendern))
                    {
                        foreach (var feld in position.Felder)
                            SetzeKind(zuAendern, feld.Name, feld.Neu);
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

    private static void SetzeKind(XElement parent, string name, string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return;

        var kind = parent.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.Ordinal));

        if (kind is not null)
        {
            kind.Value = wert;
            return;
        }

        var neu = new XElement(parent.Name.Namespace + name, wert);
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

        var platz = Array.IndexOf(KanalFeldreihenfolge, name);
        if (platz < 0)
            return null;

        foreach (var kind in parent.Elements())
        {
            var stelle = Array.IndexOf(KanalFeldreihenfolge, kind.Name.LocalName);
            if (stelle > platz)
                return kind;
        }

        return null;
    }

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
                                 && e.Elements().Any(k => string.Equals(k.Name.LocalName, name, StringComparison.Ordinal)));

        if (vorbild is null)
            return null;

        var namen = vorbild.Elements().Select(e => e.Name.LocalName).ToList();
        var platz = namen.IndexOf(name);
        if (platz < 0)
            return null;

        for (var i = platz + 1; i < namen.Count; i++)
        {
            var treffer = parent.Elements()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, namen[i], StringComparison.Ordinal));
            if (treffer is not null)
                return treffer;
        }

        return null;
    }

    private static void AktualisiereVorhandenesKind(XElement parent, string name, string wert)
    {
        var kind = parent.Elements()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.Ordinal));
        if (kind is not null)
            kind.Value = wert;
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
