using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Output;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

/// <summary>
/// Loest Vorlagen- und Logo-Pfad zentral auf und delegiert das eigentliche
/// Rendern an den <see cref="OfferHtmlToPdfRenderer"/>. Fasst den frueher in
/// zwei ViewModels duplizierten Pfadbau plus <c>new OfferHtmlToPdfRenderer()</c>
/// an einer testbaren Stelle zusammen.
/// </summary>
public sealed class OfferPdfExportService : IOfferPdfExportService
{
    private readonly Func<IOfferPdfModel, string, string, string?, CancellationToken, Task> _render;
    private readonly IBerichtsMarke? _berichtsMarke;

    public OfferPdfExportService(IBerichtsMarke? berichtsMarke = null)
        : this(
            (model, templatePath, outputPath, logoPath, ct) =>
                new OfferHtmlToPdfRenderer().RenderAsync(model, templatePath, outputPath, logoPath, ct),
            berichtsMarke)
    {
    }

    internal OfferPdfExportService(
        Func<IOfferPdfModel, string, string, string?, CancellationToken, Task> render,
        IBerichtsMarke? berichtsMarke = null)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
        _berichtsMarke = berichtsMarke;
    }

    private const string TemplateFileName = "cost_summary.sbnhtml";

    public Task ExportAsync(IOfferPdfModel model, string outputPdfPath, CancellationToken ct = default)
        => OfferPdfTemplateExport.RenderAsync(_render, TemplateFileName, model, outputPdfPath, ct, _berichtsMarke);
}
