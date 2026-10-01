namespace AuswertungPro.Next.Application.Import;

/// <summary>Woher die gefundene Protokolldatei stammt.</summary>
public enum SchachtProtocolFileOrigin
{
    /// <summary>Direkt aus der gespeicherten Verknuepfung (PDF_Path oder Link).</summary>
    Verknuepfung,

    /// <summary>Aus dem Ordner genau dieses einen Schachts gesucht.</summary>
    Schachtordner
}

/// <summary>Gefundene Protokolldatei samt Herkunft.</summary>
public sealed record SchachtProtocolFileMatch(string PdfPfad, SchachtProtocolFileOrigin Herkunft);

/// <summary>
/// Sucht die zu genau einem Schacht gehoerende Protokoll-PDF. Zuerst zaehlt die
/// gespeicherte Verknuepfung (relativ oder absolut). Erst wenn diese ins Leere
/// zeigt, wird ausschliesslich im Ordner dieses einen Schachts gesucht — nie in
/// fremden Schachtordnern und nie ausserhalb des Projekts.
/// </summary>
public interface ISchachtProtocolFileLocator
{
    SchachtProtocolFileMatch? Locate(
        string projektOrdner,
        string? gespeicherterPfad,
        string? linkPfad,
        string? schachtnummer);
}
