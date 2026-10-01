using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Ergebnis der manuellen Erzeugung einer einzelnen Dossier-Bauteilliste.</summary>
public sealed record DossierComponentListExportResult(
    bool Success,
    string Message,
    string? FilePath);

/// <summary>
/// Erzeugt Haltungs- und Schachtlisten auf ausdruecklichen Wunsch. Jede Methode
/// schreibt genau eine neue PDF-Datei und ueberschreibt keine bestehende Datei.
/// </summary>
public interface IDossierComponentListExportService
{
    Task<DossierComponentListExportResult> CreateHoldingListAsync(
        DossierExportRequest request,
        CancellationToken ct = default);

    Task<DossierComponentListExportResult> CreateShaftListAsync(
        DossierExportRequest request,
        CancellationToken ct = default);
}
