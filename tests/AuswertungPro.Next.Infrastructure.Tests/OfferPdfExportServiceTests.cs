using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert, dass der Export-Dienst Vorlagen- und Logo-Pfad zentral aufbaut und das
/// Modell an den Renderer weiterreicht — ohne echten Playwright-/PDF-Lauf. Genau
/// das war vorher nicht pruefbar, weil beide ViewModels den Renderer per new erzeugten.
/// </summary>
public sealed class OfferPdfExportServiceTests
{
    [Fact]
    public async Task ExportAsync_baut_Vorlagen_und_Logo_Pfad_und_reicht_Modell_durch()
    {
        object? seenModel = null;
        string? seenTemplate = null;
        string? seenOutput = null;
        string? seenLogo = null;

        var service = new OfferPdfExportService(
            (model, templatePath, outputPath, logoPath, _) =>
            {
                seenModel = model;
                seenTemplate = templatePath;
                seenOutput = outputPath;
                seenLogo = logoPath;
                return Task.CompletedTask;
            });

        var model = new OfferPdfModel();
        await service.ExportAsync(model, "C:/ziel/angebot.pdf");

        Assert.Same(model, seenModel);
        Assert.Equal("C:/ziel/angebot.pdf", seenOutput);
        Assert.EndsWith(Path.Combine("Templates", "cost_summary.sbnhtml"), seenTemplate);
        Assert.EndsWith(Path.Combine("Assets", "Brand", "abwasser-uri-logo.png"), seenLogo);
    }

    [Fact]
    public async Task ExportAsync_lehnt_leeren_Zielpfad_ab()
    {
        var service = new OfferPdfExportService((_, _, _, _, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportAsync(new OfferPdfModel(), "   "));
    }

    /// <summary>
    /// Optikanalyse 28.09.2026, Aufgabe 15: mit injizierter <see cref="IBerichtsMarke"/> geht
    /// deren Pfad an den Renderer — die gemeinsame Quelle statt der fest eingetragenen Datei
    /// neben dem Programm.
    /// </summary>
    [Fact]
    public async Task ExportAsync_verwendet_die_injizierte_IBerichtsMarke_statt_des_festen_Standardpfads()
    {
        string? seenLogo = null;
        var service = new OfferPdfExportService(
            (_, _, _, logoPath, _) =>
            {
                seenLogo = logoPath;
                return Task.CompletedTask;
            },
            new StubBerichtsMarke(@"D:\Firmenlogos\aktuelles-logo.png"));

        await service.ExportAsync(new OfferPdfModel(), "C:/ziel/angebot.pdf");

        Assert.Equal(@"D:\Firmenlogos\aktuelles-logo.png", seenLogo);
    }

    [Fact]
    public async Task ExportAsync_ohne_IBerichtsMarke_verwendet_weiterhin_den_festen_Standardpfad()
    {
        string? seenLogo = null;
        var service = new OfferPdfExportService(
            (_, _, _, logoPath, _) =>
            {
                seenLogo = logoPath;
                return Task.CompletedTask;
            },
            berichtsMarke: null);

        await service.ExportAsync(new OfferPdfModel(), "C:/ziel/angebot.pdf");

        Assert.EndsWith(Path.Combine("Assets", "Brand", "abwasser-uri-logo.png"), seenLogo);
    }

    private sealed class StubBerichtsMarke : IBerichtsMarke
    {
        public StubBerichtsMarke(string? logoPfad) => LogoPfad = logoPfad;
        public string? LogoPfad { get; }
    }
}
