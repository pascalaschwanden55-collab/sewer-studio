using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Ein Kanalschaden, wie er in der Original-XTF steht. Reine Leseangabe fuer die
/// Zuordnung — es wird nie etwas davon in die Projektdaten geschrieben.
/// </summary>
public sealed record XtfKanalschadenElement(
    string KanalschadenTid,
    string UntersuchungTid,
    string HaltungBezeichnung,
    string Code,
    double? Distanz,
    string? Videozaehlerstand,
    string? Quantifizierung1 = null,
    string? Quantifizierung2 = null);

/// <summary>Wie ein Befund seinem Element zugeordnet wurde.</summary>
public enum XtfZuordnungsArt
{
    /// <summary>Ueber die beim Import festgehaltene Kennung — eindeutig.</summary>
    UeberHerkunft,

    /// <summary>Ueber Code, Meter und Videozaehlerstand — fuer Altbestand ohne Kennung.</summary>
    UeberInhalt
}

public sealed record XtfFindingZuordnung(
    VsaFinding Befund,
    XtfKanalschadenElement Element,
    XtfZuordnungsArt Art);

/// <summary>
/// Ergebnis der Zuordnung. Alles, was nicht eindeutig ist, bleibt sichtbar stehen —
/// es wird nichts geraten und nichts stillschweigend weggelassen.
/// </summary>
public sealed record XtfZuordnungsErgebnis(
    IReadOnlyList<XtfFindingZuordnung> Zugeordnet,
    IReadOnlyList<VsaFinding> OhneTreffer,
    IReadOnlyList<VsaFinding> Mehrdeutig,
    IReadOnlyList<XtfKanalschadenElement> NichtVerwendet)
{
    /// <summary>True, wenn jeder Befund genau ein Element gefunden hat.</summary>
    public bool Vollstaendig => OhneTreffer.Count == 0 && Mehrdeutig.Count == 0;
}
