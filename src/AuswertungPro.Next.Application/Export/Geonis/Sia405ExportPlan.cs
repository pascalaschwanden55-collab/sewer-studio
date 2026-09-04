namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>Klasse eines zu schreibenden Objekts.</summary>
public enum Sia405ObjektArt
{
    Haltung,
    Kanal,
    Normschacht,
    Rohrprofil
}

/// <summary>Eine einzelne Attributaenderung — "alt" nur fuer das Protokoll.</summary>
public sealed record Sia405AttributAenderung(string Attribut, string? Alt, string Neu);

/// <summary>
/// Ein Objekt, das die Ausgabedatei enthaelt.
///
/// Geschrieben wird immer das vollstaendige Objekt aus dem Kataster (sonst waere die Datei
/// nicht modellgueltig); <see cref="Aenderungen"/> sagt, welche Werte davon abweichen. Welche
/// Attribute GEONIS uebernimmt, entscheidet die FME-Workbench anhand dieser Liste.
/// </summary>
public sealed class Sia405ExportObjekt
{
    public Sia405ObjektArt Art { get; init; }

    /// <summary>Elementname ohne Modellpraefix, z. B. "Haltung".</summary>
    public string Klasse { get; init; } = string.Empty;

    public string Tid { get; init; } = string.Empty;

    public string ObjId { get; init; } = string.Empty;

    public string Bezeichnung { get; init; } = string.Empty;

    public IReadOnlyList<Sia405AttributAenderung> Aenderungen { get; init; } =
        Array.Empty<Sia405AttributAenderung>();
}

/// <summary>Ein bewusst nicht uebernommenes Objekt oder Attribut samt Begruendung.</summary>
public sealed record Sia405ExportHinweis(string Objekt, string Grund);

/// <summary>
/// Ergebnis der Planung: was geschrieben wuerde und was nicht. Der Plan ist zugleich der
/// Trockenlauf — ohne ihn wird nie eine Datei erzeugt.
/// </summary>
public sealed class Sia405ExportPlan
{
    public string KatasterQuelle { get; init; } = string.Empty;

    public DateOnly AenderungsDatum { get; init; }

    public Sia405ModellAngaben Modell { get; init; } =
        new("http://www.interlis.ch/INTERLIS2.3", "2.3", string.Empty, null, Array.Empty<Sia405ModellReferenz>());

    public IReadOnlyList<Sia405ExportObjekt> Objekte { get; init; } = Array.Empty<Sia405ExportObjekt>();

    public IReadOnlyList<Sia405ExportHinweis> Hinweise { get; init; } = Array.Empty<Sia405ExportHinweis>();

    public Sia405AttributReihenfolge AttributReihenfolge { get; init; } = new();

    public int GeaenderteObjekte => Objekte.Count(o => o.Aenderungen.Count > 0);
}
