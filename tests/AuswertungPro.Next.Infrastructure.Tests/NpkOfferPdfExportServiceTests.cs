using System.Diagnostics;
using AuswertungPro.Next.Application.Output;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// NPK-Export und Drucken hinter Vertraegen.
///
/// Beide lagen vorher als Pfadbau, <c>new OfferHtmlToPdfRenderer()</c> und
/// <c>Process.Start</c> im BuilderPageViewModel und waren damit weder ersetzbar noch
/// pruefbar.
/// </summary>
public sealed class NpkOfferPdfExportServiceTests
{
    private sealed class Modell : IOfferPdfModel;

    [Fact]
    public async Task Der_NPK_Export_verwendet_die_NPK_Vorlage_und_das_Logo()
    {
        string? vorlage = null, ziel = null, logo = null;
        var dienst = new NpkOfferPdfExportService((_, t, o, l, _) =>
        {
            vorlage = t; ziel = o; logo = l;
            return Task.CompletedTask;
        });

        await dienst.ExportAsync(new Modell(), @"C:\temp\offerte.pdf");

        Assert.EndsWith(Path.Combine("Templates", "npk_offer.sbnhtml"), vorlage);
        Assert.EndsWith(Path.Combine("Assets", "Brand", "abwasser-uri-logo.png"), logo);
        Assert.Equal(@"C:\temp\offerte.pdf", ziel);
    }

    // Die beiden Offertarten duerfen sich nur in der Vorlage unterscheiden.
    [Fact]
    public async Task Der_normale_Export_verwendet_weiterhin_die_Kostenvorlage()
    {
        string? vorlage = null;
        var dienst = new OfferPdfExportService((_, t, _, _, _) =>
        {
            vorlage = t;
            return Task.CompletedTask;
        });

        await dienst.ExportAsync(new Modell(), @"C:\temp\kosten.pdf");

        Assert.EndsWith(Path.Combine("Templates", "cost_summary.sbnhtml"), vorlage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ohne_Zielpfad_wird_nicht_gerendert(string ziel)
    {
        var gerendert = false;
        var dienst = new NpkOfferPdfExportService((_, _, _, _, _) =>
        {
            gerendert = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<ArgumentException>(() => dienst.ExportAsync(new Modell(), ziel));
        Assert.False(gerendert);
    }

    [Fact]
    public void Der_Druck_uebergibt_die_Datei_an_den_Druckweg()
    {
        var datei = Path.Combine(Path.GetTempPath(), $"druck-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(datei, "%PDF-1.4");
        try
        {
            ProcessStartInfo? gestartet = null;
            new PdfPrintService(psi => gestartet = psi).Print(datei);

            Assert.NotNull(gestartet);
            Assert.Equal(datei, gestartet!.FileName);
            Assert.Equal("print", gestartet.Verb);
            Assert.True(gestartet.UseShellExecute);
        }
        finally
        {
            File.Delete(datei);
        }
    }

    // Eine klare Meldung statt einer Win32-Ausnahme aus der Tiefe der Shell.
    [Fact]
    public void Eine_fehlende_Datei_wird_nicht_an_die_Shell_gegeben()
    {
        var gestartet = false;
        var dienst = new PdfPrintService(_ => gestartet = true);

        Assert.Throws<FileNotFoundException>(
            () => dienst.Print(Path.Combine(Path.GetTempPath(), "gibt-es-nicht.pdf")));
        Assert.False(gestartet);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ohne_Dateiangabe_wird_nichts_gestartet(string pfad)
    {
        var gestartet = false;
        var dienst = new PdfPrintService(_ => gestartet = true);

        Assert.Throws<ArgumentException>(() => dienst.Print(pfad));
        Assert.False(gestartet);
    }
}
