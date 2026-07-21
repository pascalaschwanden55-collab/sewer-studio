using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Output;

/// <summary>
/// Kapselt den PDF-Export einer Kostenzusammenstellung: Vorlagen- und Logo-Pfad
/// werden zentral aufgeloest, danach wird das uebergebene Modell gerendert. Die
/// ViewModels sprechen nur diesen Vertrag an und kennen weder den konkreten
/// Renderer noch die Ablageorte der Vorlage. Das Modell ist ueber den Marker
/// <see cref="IOfferPdfModel"/> typsicher gebunden; der konkrete Modelltyp liegt
/// in Infrastructure.
/// </summary>
public interface IOfferPdfExportService
{
    /// <summary>
    /// Rendert das Modell in eine PDF-Datei unter <paramref name="outputPdfPath"/>.
    /// </summary>
    Task ExportAsync(IOfferPdfModel model, string outputPdfPath, CancellationToken ct = default);
}
