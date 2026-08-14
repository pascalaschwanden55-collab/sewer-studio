using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Output;

/// <summary>
/// Wie <see cref="IOfferPdfExportService"/>, aber fuer die NPK-135-Offerte mit ihrer
/// eigenen Vorlage.
///
/// Eigener Vertrag statt eines Vorlagenparameters: Der Aufrufer soll die Vorlage nicht
/// waehlen koennen. Vorher baute das ViewModel Vorlagen- und Logopfad selbst und erzeugte
/// direkt einen Renderer — damit lag Dateilogik wieder in der Oberflaeche.
/// </summary>
public interface INpkOfferPdfExportService
{
    /// <summary>Rendert das NPK-Modell in eine PDF-Datei unter <paramref name="outputPdfPath"/>.</summary>
    Task ExportAsync(IOfferPdfModel model, string outputPdfPath, CancellationToken ct = default);
}
