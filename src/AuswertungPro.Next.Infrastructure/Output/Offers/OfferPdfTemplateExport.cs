using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Output;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

/// <summary>
/// Gemeinsamer Pfadbau der Offert-PDFs: Vorlage und Logo liegen beide neben dem
/// Programm. Nur die Vorlagendatei unterscheidet die Offertarten, alles andere ist
/// gleich — deshalb steht es einmal hier statt zweimal in den Diensten.
/// </summary>
internal static class OfferPdfTemplateExport
{
    public static Task RenderAsync(
        Func<IOfferPdfModel, string, string, string?, CancellationToken, Task> render,
        string templateFileName,
        IOfferPdfModel model,
        string outputPdfPath,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(outputPdfPath))
            throw new ArgumentException("Zielpfad fehlt.", nameof(outputPdfPath));

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", templateFileName);
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");

        return render(model, templatePath, outputPdfPath, logoPath, ct);
    }
}
