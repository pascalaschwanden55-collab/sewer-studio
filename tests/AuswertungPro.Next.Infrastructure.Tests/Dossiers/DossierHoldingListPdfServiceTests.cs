using System.Globalization;
using System.Text;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Dossiers;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierHoldingListPdfServiceTests
{
    [Theory]
    [InlineData("Schmutzabwasser", "#7A6242", "#F5F0E8")]
    [InlineData("Schmutzwasser", "#7A6242", "#F5F0E8")]
    [InlineData("Niederschlagsabwasser", "#4A7FA5", "#EBF2F7")]
    [InlineData("Regenwasser", "#4A7FA5", "#EBF2F7")]
    [InlineData("Reinabwasser", "#4A7FA5", "#EBF2F7")]
    [InlineData("Mischabwasser", "#8E4A6E", "#F5ECF1")]
    [InlineData("entlastetes Mischabwasser", "#8E4A6E", "#F5ECF1")]
    [InlineData("unbekannt", "#7A8A94", "#F2F4F5")]
    [InlineData("", "#7A8A94", "#F2F4F5")]
    public void Nutzungsarten_verwenden_in_allen_Berichten_dieselbe_Farbe(
        string value,
        string expectedAccent,
        string expectedLight)
    {
        var colors = NutzungsartReportColors.Resolve(value);

        Assert.Equal(expectedAccent, colors.Accent);
        Assert.Equal(expectedLight, colors.Light);
        Assert.Equal(expectedAccent, ProtocolPdfExporter.ResolveNutzungsartBrand(value));
        Assert.Equal(expectedLight, ProtocolPdfExporter.ResolveNutzungsartBrandLight(expectedAccent));
    }

    [Theory]
    [InlineData("0", "#FF0000", "#FFFFFF")]
    [InlineData("1", "#FF6600", "#FFFFFF")]
    [InlineData("2", "#FFFF00", "#1F2937")]
    [InlineData("3", "#AEB135", "#1F2937")]
    [InlineData("4", "#92D050", "#1F2937")]
    public void Zustandsklassen_verwenden_die_verbindliche_Berichtspalette(
        string value,
        string expectedBackground,
        string expectedForeground)
        => Assert.Equal(
            (expectedBackground, expectedForeground),
            DossierHoldingListPdfService.ResolveConditionColors(value));

    [Fact]
    public void Zusammenfassung_formatiert_Zustand_und_Laenge_wie_im_freigegebenen_Muster()
    {
        var lines = new[]
        {
            Line("A", condition: "4"),
            Line("B", condition: "2"),
            Line("C", condition: "ohne")
        };

        Assert.Equal("Z2 bis Z4", DossierHoldingListPdfService.ResolveConditionSpan(lines));
        Assert.Equal("3,60 m", DossierHoldingListPdfService.FormatLength(3.6));
        Assert.Equal("nicht erfasst", DossierHoldingListPdfService.FormatLength(null));
        Assert.Equal("nicht erfasst", DossierHoldingListPdfService.FormatTotalLength([]));
        Assert.Equal("0,00 m", DossierHoldingListPdfService.FormatTotalLength(
            [Line("Null", length: 0)]));
    }

    [Fact]
    public void CreatePdf_erzeugt_A4_mit_Dossierdaten_und_ohne_Musterkennzeichnung()
    {
        var model = new DossierHoldingListPdfModel(
            "Feldliweg 26",
            "Heinz Eduard Josef Müller",
            "Feldliweg 26, 6460 Altdorf",
            new DateTime(2026, 8, 28),
            [
                Line("77467-77463", "Beton", "300", 3.6, "2", "Mischabwasser"),
                Line("77566-77474", "PVC", "250", 3.4, "4", "Niederschlagsabwasser")
            ],
            MissingHoldingCount: 0);
        var service = ServiceWithoutAssets();

        var pdf = service.CreatePdf(model);

        Assert.True(pdf.Length > 5_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        using var document = PdfDocument.Open(pdf);
        var page = Assert.Single(document.GetPages());
        Assert.InRange(page.Width, 594, 596);
        Assert.InRange(page.Height, 841, 843);

        var text = PageText(page);
        Assert.Contains("Haltungsliste", text, StringComparison.Ordinal);
        Assert.Contains("Ergänzung zum Eigentümerdossier", text, StringComparison.Ordinal);
        Assert.Contains("Heinz Eduard Josef Müller", text, StringComparison.Ordinal);
        Assert.Contains("Feldliweg 26, 6460 Altdorf", text, StringComparison.Ordinal);
        Assert.Contains("28.08.2026", text, StringComparison.Ordinal);
        Assert.Contains("77467-77463", text, StringComparison.Ordinal);
        Assert.Contains("Beton", text, StringComparison.Ordinal);
        Assert.Contains("PVC", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Polyvinylchlorid", text, StringComparison.Ordinal);
        Assert.Contains("300", text, StringComparison.Ordinal);
        Assert.Contains("3,60 m", text, StringComparison.Ordinal);
        Assert.Contains("Z2", text, StringComparison.Ordinal);
        Assert.Contains("Mischabwasser", text, StringComparison.Ordinal);
        Assert.Contains("Niederschlagsabwasser", text, StringComparison.Ordinal);
        Assert.Contains("Farblegende", text, StringComparison.Ordinal);
        Assert.Contains("nicht erfasst", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MUSTER", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEISPIELDATEN", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leere_und_fehlende_Daten_werden_ehrlich_ausgewiesen()
    {
        var model = new DossierHoldingListPdfModel(
            "Leeres Dossier",
            "",
            "",
            new DateTime(2026, 8, 28),
            [],
            MissingHoldingCount: 1);

        var pdf = ServiceWithoutAssets().CreatePdf(model);

        using var document = PdfDocument.Open(pdf);
        var text = PageText(Assert.Single(document.GetPages()));
        Assert.Contains("keine Haltungen zugeordnet", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht erfasst", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht mehr vorhanden", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Viele_Haltungen_fliessen_auf_mehrere_Seiten_mit_wiederholtem_Tabellenkopf()
    {
        var lines = Enumerable.Range(1, 60)
            .Select(index => Line(
                "H-" + index.ToString("000", CultureInfo.InvariantCulture),
                "PP",
                "250",
                index + 0.25,
                (index % 5).ToString(CultureInfo.InvariantCulture),
                index % 2 == 0 ? "Schmutzabwasser" : "Niederschlagsabwasser"))
            .ToList();
        var model = new DossierHoldingListPdfModel(
            "Mehrseitig",
            "Eigentümer",
            "Musterweg 1, 6460 Altdorf",
            new DateTime(2026, 8, 28),
            lines,
            MissingHoldingCount: 0);

        var pdf = ServiceWithoutAssets().CreatePdf(model);

        using var document = PdfDocument.Open(pdf);
        Assert.True(document.NumberOfPages > 1);
        var pagesWithRows = document.GetPages()
            .Select(PageText)
            .Where(text => text.Contains("H-", StringComparison.Ordinal))
            .ToList();
        Assert.True(pagesWithRows.Count > 1);
        Assert.All(pagesWithRows, text =>
        {
            Assert.Contains("Haltung", text, StringComparison.Ordinal);
            Assert.Contains("Nutzungsart", text, StringComparison.Ordinal);
        });

        var allText = string.Join(" ", document.GetPages().Select(PageText));
        foreach (var line in lines)
            Assert.Equal(1, CountOccurrences(allText, line.HoldingName));
    }

    private static DossierHoldingListPdfService ServiceWithoutAssets()
        => new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

    private static DossierHoldingLine Line(
        string name,
        string material = "",
        string dn = "",
        double? length = null,
        string condition = "",
        string usage = "")
        => new(Guid.NewGuid(), name, "", length, condition, 0m, "")
        {
            PipeMaterial = material,
            NominalDiameterMm = dn,
            UsageType = usage
        };

    private static string PageText(UglyToad.PdfPig.Content.Page page)
        => string.Join(" ", page.GetWords().Select(word => word.Text));

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
