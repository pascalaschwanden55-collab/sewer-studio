namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Liefert die vom Benutzer eingestellte Seitenaufteilung der selbst erzeugten
/// Haltungsprotokolle. Die Umsetzung liegt in der UI-Schicht auf den Einstellungen.
/// </summary>
public interface IProtocolPdfLayoutSettings
{
    /// <summary>Anzahl Fotos je Fotoseite (1, 2, 4 oder 6).</summary>
    int PhotosPerPage { get; }
}
