using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Erzeugt den Plan fuer eine revidierte XTF: den vollstaendigen Vergleich zwischen dem,
/// was in der Originaldatei steht, und dem, was im Projekt aktuell gilt.
///
/// Reine Rechnung — kein Dateizugriff, keine Mutation. Weder Projektdaten noch die XTF
/// werden angefasst. Geschrieben wird erst spaeter, und nur nach diesem Plan.
///
/// Der Vergleich laeuft ueber zwei Ketten, damit auch eine Codekorrektur richtig als
/// Aenderung erkannt wird statt als "geloescht plus neu":
///   Arbeitsstand --(feste Eintrags-ID)--> Ausgangsstand --(Inhalt)--> Element der Datei
/// Der Ausgangsstand bleibt unangetastet und passt deshalb weiterhin zur Datei.
/// </summary>
public static class XtfRevisionPlanBuilder
{
    public static XtfRevisionPlan Build(
        IEnumerable<HaltungRecord> haltungen,
        IReadOnlyList<XtfKanalschadenElement> elemente,
        string quelldatei)
    {
        ArgumentNullException.ThrowIfNull(haltungen);
        ArgumentNullException.ThrowIfNull(elemente);

        var positionen = new List<XtfRevisionPosition>();
        var warnungen = new List<string>();

        foreach (var record in haltungen)
        {
            var kandidaten = XtfFindingMatcher.ElementeDerHaltung(record, elemente).ToList();

            // Kommt diese Haltung in dieser Datei nicht vor, hat sie hier nichts zu suchen.
            // Ohne diese Grenze wuerde jede von Hand ergaenzte Codierung eines fremden
            // Gebiets als "neu" auftauchen und eine Revision melden, die es nicht gibt.
            // Ein Projekt enthaelt in der Regel mehrere XTF aus verschiedenen Gebieten.
            if (kandidaten.Count == 0)
                continue;

            BaueHaltung(record, kandidaten, positionen, warnungen);
        }

        return new XtfRevisionPlan(quelldatei ?? "", positionen, warnungen);
    }

    private static void BaueHaltung(
        HaltungRecord record,
        IReadOnlyList<XtfKanalschadenElement> kandidaten,
        List<XtfRevisionPosition> positionen,
        List<string> warnungen)
    {
        var haltung = record.GetFieldValue(FieldKeys.HoldingName) ?? "";
        var untersuchung = record.XtfHerkunft?.UntersuchungTid
                           ?? kandidaten.FirstOrDefault()?.UntersuchungTid
                           ?? "";

        // Ohne Protokoll gibt es keine Aussage ueber die Codierung: alles bleibt, wie es ist.
        if (record.Protocol is null)
        {
            foreach (var element in kandidaten)
                positionen.Add(Position(XtfRevisionAenderung.Unveraendert, element, haltung));
            return;
        }

        var ausgang = Lebende(record.Protocol.Original?.Entries);
        var arbeit = Lebende(record.Protocol.Current?.Entries);

        // Ausgangsstand gegen die Datei: der Ausgangsstand ist unveraendert und passt inhaltlich.
        var zurDatei = XtfEindeutigeZuordnung.Bilde(ausgang, kandidaten, PasstZuElement);
        foreach (var offen in zurDatei.Mehrdeutig)
        {
            warnungen.Add(
                $"{haltung}: Der importierte Befund '{offen.Code}' bei {Meter(offen.MeterStart)} " +
                "passt auf mehrere Eintraege der Originaldatei. Bitte von Hand entscheiden.");
        }

        var elementJeEintrag = zurDatei.Zugeordnet;
        var behandelteElemente = new HashSet<string>(
            elementJeEintrag.Values.Select(e => e.KanalschadenTid),
            StringComparer.Ordinal);

        var ausgangJeId = ausgang.ToDictionary(e => e.EntryId);

        foreach (var eintrag in arbeit)
        {
            // Von Hand ergaenzt: kommt neu in die Datei.
            if (!ausgangJeId.TryGetValue(eintrag.EntryId, out var vorher))
            {
                positionen.Add(new XtfRevisionPosition(
                    XtfRevisionAenderung.Neu,
                    KanalschadenTid: null,
                    untersuchung,
                    haltung,
                    (eintrag.Code ?? "").Trim(),
                    eintrag.MeterStart,
                    NeuFelder(eintrag)));
                continue;
            }

            // Bekannt, aber ohne sicheres Gegenstueck in der Datei: nicht anfassen.
            if (!elementJeEintrag.TryGetValue(vorher, out var element))
                continue;

            var felder = Unterschiede(element, eintrag);
            positionen.Add(new XtfRevisionPosition(
                felder.Count == 0 ? XtfRevisionAenderung.Unveraendert : XtfRevisionAenderung.Geaendert,
                element.KanalschadenTid,
                untersuchung,
                haltung,
                (eintrag.Code ?? "").Trim(),
                eintrag.MeterStart,
                felder));
        }

        // Im Ausgangsstand vorhanden, im Arbeitsstand nicht mehr: entfernen.
        var arbeitIds = new HashSet<Guid>(arbeit.Select(e => e.EntryId));
        foreach (var (eintrag, element) in elementJeEintrag)
        {
            if (arbeitIds.Contains(eintrag.EntryId))
                continue;

            positionen.Add(Position(XtfRevisionAenderung.Entfernt, element, haltung));
        }

        // Elemente ohne jede Zuordnung bleiben unveraendert stehen — sie werden nie
        // stillschweigend entfernt, nur weil die Zuordnung nicht gelang.
        foreach (var element in kandidaten)
        {
            if (behandelteElemente.Contains(element.KanalschadenTid))
                continue;

            positionen.Add(Position(XtfRevisionAenderung.Unveraendert, element, haltung));
        }
    }

    private static List<ProtocolEntry> Lebende(IEnumerable<ProtocolEntry>? entries)
        => entries?.Where(e => !e.IsDeleted).ToList() ?? new List<ProtocolEntry>();

    private static XtfRevisionPosition Position(
        XtfRevisionAenderung art,
        XtfKanalschadenElement element,
        string haltung)
        => new(
            art,
            element.KanalschadenTid,
            element.UntersuchungTid,
            haltung,
            element.Code,
            element.Distanz,
            Array.Empty<XtfRevisionFeld>());

    private static bool PasstZuElement(ProtocolEntry eintrag, XtfKanalschadenElement element)
    {
        var code = (eintrag.Code ?? "").Trim();
        if (code.Length == 0
            || !string.Equals(code, (element.Code ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!GleicherMeter(eintrag.MeterStart, element.Distanz))
            return false;

        var video = (eintrag.Mpeg ?? "").Trim();
        var elementVideo = (element.Videozaehlerstand ?? "").Trim();
        if (video.Length > 0 && elementVideo.Length > 0)
            return string.Equals(video, elementVideo, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private static List<XtfRevisionFeld> Unterschiede(XtfKanalschadenElement element, ProtocolEntry eintrag)
    {
        var felder = new List<XtfRevisionFeld>();

        var code = (eintrag.Code ?? "").Trim();
        if (!string.Equals(code, (element.Code ?? "").Trim(), StringComparison.Ordinal))
            felder.Add(new XtfRevisionFeld("KanalSchadencode", element.Code, code));

        if (!GleicherMeter(eintrag.MeterStart, element.Distanz))
            felder.Add(new XtfRevisionFeld("Distanz", Meter(element.Distanz), Meter(eintrag.MeterStart)));

        var video = (eintrag.Mpeg ?? "").Trim();
        var elementVideo = (element.Videozaehlerstand ?? "").Trim();
        if (video.Length > 0 && !string.Equals(video, elementVideo, StringComparison.Ordinal))
            felder.Add(new XtfRevisionFeld("Videozaehlerstand", element.Videozaehlerstand, video));

        VergleicheQuantifizierung(felder, "Quantifizierung1", element.Quantifizierung1, eintrag);
        VergleicheQuantifizierung(felder, "Quantifizierung2", element.Quantifizierung2, eintrag);

        return felder;
    }

    private static void VergleicheQuantifizierung(
        List<XtfRevisionFeld> felder,
        string name,
        string? alt,
        ProtocolEntry eintrag)
    {
        if (eintrag.CodeMeta?.Parameters is null
            || !eintrag.CodeMeta.Parameters.TryGetValue(name, out var neu))
        {
            return;
        }

        neu = (neu ?? "").Trim();
        var vorher = (alt ?? "").Trim();

        // Ein leerer neuer Wert loescht nichts: Ohne Angabe bleibt der Originalwert stehen.
        if (neu.Length == 0 || string.Equals(neu, vorher, StringComparison.Ordinal))
            return;

        felder.Add(new XtfRevisionFeld(name, alt, neu));
    }

    private static List<XtfRevisionFeld> NeuFelder(ProtocolEntry eintrag)
    {
        var felder = new List<XtfRevisionFeld>
        {
            new("KanalSchadencode", null, (eintrag.Code ?? "").Trim())
        };

        if (eintrag.MeterStart.HasValue)
            felder.Add(new XtfRevisionFeld("Distanz", null, Meter(eintrag.MeterStart)));

        var video = (eintrag.Mpeg ?? "").Trim();
        if (video.Length > 0)
            felder.Add(new XtfRevisionFeld("Videozaehlerstand", null, video));

        foreach (var name in new[] { "Quantifizierung1", "Quantifizierung2" })
        {
            if (eintrag.CodeMeta?.Parameters is not null
                && eintrag.CodeMeta.Parameters.TryGetValue(name, out var wert)
                && !string.IsNullOrWhiteSpace(wert))
            {
                felder.Add(new XtfRevisionFeld(name, null, wert.Trim()));
            }
        }

        return felder;
    }

    /// <summary>Die XTF fuehrt die Distanz mit zwei Nachkommastellen; so wird auch verglichen.</summary>
    private static bool GleicherMeter(double? a, double? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;

        return Math.Round(a.Value, 2) == Math.Round(b.Value, 2);
    }

    private static string? Meter(double? wert)
        => wert?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
