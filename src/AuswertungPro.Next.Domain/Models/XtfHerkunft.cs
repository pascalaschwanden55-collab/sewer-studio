namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Bindung einer Haltung an die XTF-Datei, aus der sie eingelesen wurde.
///
/// Wird beim Import festgehalten — dem einzigen Moment, in dem diese Zuordnung
/// sicher bekannt ist. Ohne sie waere beim spaeteren Erzeugen einer revidierten
/// XTF nicht bestimmt, gegen welche Datei revidiert wird: Im Importordner liegen
/// in der Regel mehrere.
///
/// Rein zusaetzliche Angabe. Altprojekte ohne diesen Abschnitt laden unveraendert,
/// dort bleibt sie leer und die Zuordnung wird beim Export aus dem unveraenderten
/// Original-Protokollstand hergestellt.
/// </summary>
public sealed class XtfHerkunft
{
    /// <summary>Dateiname der XTF-Quelle, ohne Pfad (der Ordner kann sich aendern).</summary>
    public string Datei { get; set; } = "";

    /// <summary>Modellname aus der HEADERSECTION, z. B. "VSA_KEK_2020_LV95".</summary>
    public string Modell { get; set; } = "";

    /// <summary>TID der Untersuchung, aus der diese Haltung entstanden ist.</summary>
    public string UntersuchungTid { get; set; } = "";
}
