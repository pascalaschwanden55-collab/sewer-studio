namespace AuswertungPro.Next.Application.Xtf;

/// <summary>Was mit einem Kanalschaden in der Revision geschieht.</summary>
public enum XtfRevisionAenderung
{
    Unveraendert,
    Geaendert,
    Neu,
    Entfernt
}

/// <summary>Ein einzelnes geaendertes Feld mit Alt- und Neuwert — fuer den Pruefbericht.</summary>
public sealed record XtfRevisionFeld(string Name, string? Alt, string? Neu);

/// <summary>
/// Eine Position des Plans: genau ein Kanalschaden und was mit ihm geschieht.
/// <see cref="KanalschadenTid"/> ist bei <see cref="XtfRevisionAenderung.Neu"/> leer —
/// die Kennung vergibt erst der Ausfuehrer.
/// </summary>
public sealed record XtfRevisionPosition(
    XtfRevisionAenderung Art,
    string? KanalschadenTid,
    string UntersuchungTid,
    string HaltungName,
    string Code,
    double? Meter,
    IReadOnlyList<XtfRevisionFeld> Felder);

/// <summary>
/// Der unveraenderliche Plan fuer eine revidierte XTF: eine vollstaendige Liste dessen,
/// was geschehen soll — bevor irgendetwas geschrieben wird.
///
/// Nach demselben Muster wie der plan-gesteuerte YOLO-Export: Erst entsteht genau ein
/// Plan, danach schreibt der Ausfuehrer ausschliesslich diesen Plan und trifft keine
/// eigenen Entscheidungen mehr.
///
/// <see cref="Warnungen"/> enthaelt alles, was nicht eindeutig zugeordnet werden konnte.
/// Solange dort etwas steht, entscheidet der Mensch — es wird nichts geraten.
/// </summary>
public sealed record XtfRevisionPlan(
    string Quelldatei,
    IReadOnlyList<XtfRevisionPosition> Positionen,
    IReadOnlyList<string> Warnungen)
{
    public int AnzahlUnveraendert => Zaehle(XtfRevisionAenderung.Unveraendert);
    public int AnzahlGeaendert => Zaehle(XtfRevisionAenderung.Geaendert);
    public int AnzahlNeu => Zaehle(XtfRevisionAenderung.Neu);
    public int AnzahlEntfernt => Zaehle(XtfRevisionAenderung.Entfernt);

    /// <summary>True, wenn die Revision nichts am Original aendern wuerde.</summary>
    public bool OhneAenderung
        => AnzahlGeaendert == 0 && AnzahlNeu == 0 && AnzahlEntfernt == 0;

    /// <summary>True, wenn etwas offen ist und der Mensch entscheiden muss.</summary>
    public bool BrauchtEntscheidung => Warnungen.Count > 0;

    private int Zaehle(XtfRevisionAenderung art)
        => Positionen.Count(p => p.Art == art);
}
