namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Ein Anschluss eines Schachts, wie ihn die Anschlusstabelle des Schachtprotokolls
/// (Uri-Formular: Nr, Aus/Ein, DN mm, Tiefe m, Material) oder SchachtPro (zusaetzlich Uhr
/// und Richtung) nennt.
///
/// Additiv am <see cref="SchachtRecord"/>, KEIN Feld: keine Tabellenspalte, kein Export,
/// keine XTF. Die Tiefe ist der Abstich ab Deckel-Oberkante bis zur Rohrsohle in Metern —
/// dieselbe Bezugskante wie <c>Schachttiefe</c>. Was die Quelle nicht nennt, bleibt
/// <c>null</c>; nichts wird geschaetzt.
/// </summary>
public sealed class SchachtAnschluss
{
    /// <summary>Laufende Nummer im Protokoll (1 = meist der Auslauf).</summary>
    public int Nr { get; set; }

    /// <summary>"Einlauf" oder "Auslauf", so wie es die Quelle schreibt.</summary>
    public string Art { get; set; } = "";

    public int? DnMm { get; set; }

    /// <summary>Tiefe der Rohrsohle ab Deckel-Oberkante in Metern.</summary>
    public decimal? TiefeM { get; set; }

    public string? Material { get; set; }

    /// <summary>Uhrlage am Umfang, falls die Quelle sie kennt (SchachtPro).</summary>
    public string? Uhr { get; set; }

    /// <summary>Freie Richtungsangabe der Quelle (SchachtPro), unveraendert.</summary>
    public string? Richtung { get; set; }

    /// <summary>Haltungsname, wenn die Quelle ihn nennt; sonst ordnet die Anzeige spaeter zu.</summary>
    public string? Haltungsname { get; set; }

    /// <summary>
    /// Zustand des Anschlusses, wie ihn die Quelle nennt: «in Ordnung»,
    /// «Mangelhaft eingebunden», «Einragend», mehrere mit « • » getrennt.
    /// <c>null</c> heisst «nicht erfasst» und ist NICHT dasselbe wie «in Ordnung».
    /// </summary>
    public string? Zustand { get; set; }

    /// <summary>
    /// true, wenn die Quelle den Zustandstext sichtbar gekuerzt hat. SchachtPro schneidet
    /// eine zu lange Zelle im PDF mit «…» ab; die weiteren Befunde stehen dann nirgends im
    /// Dokument. Der gelesene Anfang bleibt erhalten, gilt aber als unvollstaendig.
    /// </summary>
    public bool ZustandUnvollstaendig { get; set; }

    /// <summary>Herkunft: "PDF" oder "SchachtPro".</summary>
    public string? Quelle { get; set; }

    public bool IstEinlauf => Art.StartsWith("Ein", StringComparison.OrdinalIgnoreCase);

    public bool IstAuslauf => Art.StartsWith("Aus", StringComparison.OrdinalIgnoreCase);
}
