using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Output;

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

    public OfferPdfExportService()
        : this((model, templatePath, outputPath, logoPath, ct) =>
            new OfferHtmlToPdfRenderer().RenderAsync(model, templatePath, outputPath, logoPath, ct))
    {
    }

    internal OfferPdfExportService(
        Func<IOfferPdfModel, string, string, string?, CancellationToken, Task> render)
        => _render = render ?? throw new ArgumentNullException(nameof(render));

    public Task ExportAsync(IOfferPdfModel model, string outputPdfPath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(outputPdfPath))
            throw new ArgumentException("Zielpfad fehlt.", nameof(outputPdfPath));

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "cost_summary.sbnhtml");
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

        return _render(model, templatePath, outputPdfPath, logoPath, ct);
    }
}
