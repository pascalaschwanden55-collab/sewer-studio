using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Output;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

/// <summary>
/// PDF-Export der NPK-135-Offerte. Gleicher Aufbau wie
/// <see cref="OfferPdfExportService"/>, nur mit der NPK-Vorlage.
///
/// Ersetzt den frueheren Weg im <c>BuilderPageViewModel</c>, das Vorlagen- und Logopfad
/// selbst zusammensetzte und direkt einen <see cref="OfferHtmlToPdfRenderer"/> erzeugte.
/// </summary>
public sealed class NpkOfferPdfExportService : INpkOfferPdfExportService
{
    private const string TemplateFileName = "npk_offer.sbnhtml";

    private readonly Func<IOfferPdfModel, string, string, string?, CancellationToken, Task> _render;
    private readonly IBerichtsMarke? _berichtsMarke;

    public NpkOfferPdfExportService(IBerichtsMarke? berichtsMarke = null)
        : this(
            (model, templatePath, outputPath, logoPath, ct) =>
                new OfferHtmlToPdfRenderer().RenderAsync(model, templatePath, outputPath, logoPath, ct),
            berichtsMarke)
    {
    }

    /// <summary>Test-Naht: erlaubt das Rendern ohne echten Renderer.</summary>
    internal NpkOfferPdfExportService(
        Func<IOfferPdfModel, string, string, string?, CancellationToken, Task> render,
        IBerichtsMarke? berichtsMarke = null)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
        _berichtsMarke = berichtsMarke;
    }

    public Task ExportAsync(IOfferPdfModel model, string outputPdfPath, CancellationToken ct = default)
        => OfferPdfTemplateExport.RenderAsync(_render, TemplateFileName, model, outputPdfPath, ct, _berichtsMarke);
}
