using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Ordnet die gespeicherten Befunde einer Haltung den Kanalschaden-Elementen der
/// Original-XTF zu. Reine Rechnung ohne Dateizugriff und ohne jede Mutation:
/// Weder Projektdaten noch die XTF werden angefasst.
///
/// Zwei Wege, in dieser Reihenfolge:
/// 1. Ueber die beim Import festgehaltene Kennung (neue Importe) — eindeutig.
/// 2. Ueber den Inhalt: Code, Meter und Videozaehlerstand (Altbestand ohne Kennung).
///
/// Der Inhaltsweg zaehlt nur bei beidseitiger Eindeutigkeit: Genau ein Element passt
/// zum Befund UND genau ein Befund passt zu diesem Element. Alles andere gilt als
/// mehrdeutig und wird dem Menschen vorgelegt, statt geraten zu werden. Dadurch ist
/// das Ergebnis unabhaengig von der Reihenfolge der Befunde.
/// </summary>
public static class XtfFindingMatcher
{
    public static XtfZuordnungsErgebnis Match(
        HaltungRecord record,
        IReadOnlyList<XtfKanalschadenElement> elemente)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(elemente);

        var befunde = record.VsaFindings ?? new List<VsaFinding>();
        var kandidaten = ElementeDerHaltung(record, elemente).ToList();

        var zugeordnet = new List<XtfFindingZuordnung>();
        var offen = new List<VsaFinding>();
        var vergeben = new HashSet<string>(StringComparer.Ordinal);

        // Weg 1: ueber die Kennung aus dem Import.
        foreach (var befund in befunde)
        {
            var tid = (befund.KanalschadenTid ?? string.Empty).Trim();
            if (tid.Length == 0)
            {
                offen.Add(befund);
                continue;
            }

            var treffer = kandidaten.FirstOrDefault(
                e => string.Equals(e.KanalschadenTid, tid, StringComparison.Ordinal));
            if (treffer is null || !vergeben.Add(treffer.KanalschadenTid))
            {
                offen.Add(befund);
                continue;
            }

            zugeordnet.Add(new XtfFindingZuordnung(befund, treffer, XtfZuordnungsArt.UeberHerkunft));
        }

        // Weg 2: ueber den Inhalt — beidseitige Eindeutigkeit ueber die gemeinsame Regel.
        var frei = kandidaten.Where(e => !vergeben.Contains(e.KanalschadenTid)).ToList();
        var paarung = XtfEindeutigeZuordnung.Bilde(offen, frei, PasstInhaltlich);

        foreach (var (befund, element) in paarung.Zugeordnet)
        {
            vergeben.Add(element.KanalschadenTid);
            zugeordnet.Add(new XtfFindingZuordnung(befund, element, XtfZuordnungsArt.UeberInhalt));
        }

        var nichtVerwendet = kandidaten
            .Where(e => !vergeben.Contains(e.KanalschadenTid))
            .ToList();

        return new XtfZuordnungsErgebnis(
            zugeordnet,
            paarung.OhneTreffer,
            paarung.Mehrdeutig,
            nichtVerwendet);
    }

    /// <summary>
    /// Elemente dieser Haltung. Die beim Import festgehaltene Untersuchung hat Vorrang;
    /// fuer Altbestand ohne Anker wird ueber den Haltungsnamen zugeordnet.
    /// </summary>
    public static IEnumerable<XtfKanalschadenElement> ElementeDerHaltung(
        HaltungRecord record,
        IReadOnlyList<XtfKanalschadenElement> elemente)
    {
        var untersuchung = (record.XtfHerkunft?.UntersuchungTid ?? string.Empty).Trim();
        if (untersuchung.Length > 0)
        {
            return elemente.Where(
                e => string.Equals(e.UntersuchungTid, untersuchung, StringComparison.Ordinal));
        }

        var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
        if (name.Length == 0)
            return Array.Empty<XtfKanalschadenElement>();

        return elemente.Where(
            e => string.Equals((e.HaltungBezeichnung ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PasstInhaltlich(VsaFinding befund, XtfKanalschadenElement element)
    {
        var code = (befund.KanalSchadencode ?? string.Empty).Trim();
        if (code.Length == 0
            || !string.Equals(code, (element.Code ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!PasstMeter(befund.MeterStart ?? befund.SchadenlageAnfang, element.Distanz))
            return false;

        // Videozaehlerstand nur pruefen, wenn er auf beiden Seiten vorhanden ist.
        var video = (befund.MPEG ?? string.Empty).Trim();
        var elementVideo = (element.Videozaehlerstand ?? string.Empty).Trim();
        if (video.Length > 0 && elementVideo.Length > 0)
            return string.Equals(video, elementVideo, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    /// <summary>
    /// Die XTF fuehrt die Distanz mit zwei Nachkommastellen. Verglichen wird auf
    /// derselben Genauigkeit; fehlt der Wert auf beiden Seiten, gilt das als gleich.
    /// </summary>
    private static bool PasstMeter(double? befundMeter, double? elementMeter)
    {
        if (befundMeter is null && elementMeter is null)
            return true;
        if (befundMeter is null || elementMeter is null)
            return false;

        return Math.Round(befundMeter.Value, 2) == Math.Round(elementMeter.Value, 2);
    }
}
