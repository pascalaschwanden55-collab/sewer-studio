using System.Globalization;
using System.Text;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierShaftListPdfServiceTests
{
    [Fact]
    public void Modell_builder_uebernimmt_Kopf_Reihenfolge_und_Fehlstellen()
    {
        var first = Shaft("S-01", "Bahnhofstrasse", "Kontrollschacht", "2");
        var second = Shaft("S-02", "Dorfstrasse", "Einlaufschacht", "4");
        var project = new Project();
        project.SchaechteData.Add(first);
        project.SchaechteData.Add(second);

        var dossier = new DossierDefinition
        {
            Name = "Liegenschaft Feldliweg 26",
            Address = "Feldliweg",
            HouseNumbers = "26",
            PostalCode = "6460",
            Town = "Altdorf",
            Owners = { new DossierOwnerRow { Name = "Heinz Müller" } },
            ShaftNumbers = { "S-02", "S-01", "nicht-mehr-da" }
        };
        var snapshot = DossierSnapshotBuilder.Build(
            dossier,
            project,
            new ProjectCostStore());
        var stand = new DateTime(2026, 8, 28);

        var model = DossierShaftListPdfModelBuilder.Build(dossier, snapshot, stand);

        Assert.Equal("Heinz Müller", model.OwnerName);
        Assert.Equal("Feldliweg 26, 6460 Altdorf", model.PropertyAddress);
        Assert.Equal(stand, model.Stand);
        Assert.Equal(new[] { "S-02", "S-01" }, model.Shafts.Select(shaft => shaft.Number));
        Assert.Equal("Einlaufschacht", model.Shafts[0].Funktion);
        Assert.Equal(1, model.MissingShaftCount);
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
            DossierShaftListPdfService.ResolveConditionColors(value));

    [Fact]
    public void Zusammenfassung_ignoriert_ungueltige_Zustandswerte()
    {
        var lines = new[]
        {
            Line("S-01", condition: "Z4"),
            Line("S-02", condition: "2"),
            Line("S-03", condition: "ohne")
        };

        Assert.Equal("Z2 bis Z4", DossierShaftListPdfService.ResolveConditionSpan(lines));
        Assert.Equal("nicht erfasst", DossierShaftListPdfService.ResolveConditionSpan([]));
        Assert.Null(DossierShaftListPdfService.ResolveConditionColors("Z5"));
    }

    [Fact]
    public void CreatePdf_erzeugt_A4_mit_Schachtdaten_und_ehrlichen_Fehlwerten()
    {
        var model = new DossierShaftListPdfModel(
            "Feldliweg 26",
            "Heinz Eduard Josef Müller",
            "Feldliweg 26, 6460 Altdorf",
            new DateTime(2026, 8, 28),
            [
                Line("80551", "Feldliweg", "Kontrollschacht", "2"),
                Line("80552", "", "", "")
            ],
            MissingShaftCount: 0);

        var pdf = ServiceWithoutAssets().CreatePdf(model);

        Assert.True(pdf.Length > 5_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        using var document = PdfDocument.Open(pdf);
        var page = Assert.Single(document.GetPages());
        Assert.InRange(page.Width, 594, 596);
        Assert.InRange(page.Height, 841, 843);

        var text = PageText(page);
        Assert.Contains("Schachtliste", text, StringComparison.Ordinal);
        Assert.Contains("Ergänzung zum Eigentümerdossier", text, StringComparison.Ordinal);
        Assert.Contains("Heinz Eduard Josef Müller", text, StringComparison.Ordinal);
        Assert.Contains("Feldliweg 26, 6460 Altdorf", text, StringComparison.Ordinal);
        Assert.Contains("28.08.2026", text, StringComparison.Ordinal);
        Assert.Contains("80551", text, StringComparison.Ordinal);
        Assert.Contains("Kontrollschacht", text, StringComparison.Ordinal);
        Assert.Contains("Z2", text, StringComparison.Ordinal);
        Assert.Contains("nicht erfasst", text, StringComparison.Ordinal);
        Assert.Contains("Farblegende", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Leere_Liste_und_verlorene_Zuordnung_werden_sichtbar_gemeldet()
    {
        var model = new DossierShaftListPdfModel(
            "Leeres Dossier",
            "",
            "",
            new DateTime(2026, 8, 28),
            [],
            MissingShaftCount: 1);

        var pdf = ServiceWithoutAssets().CreatePdf(model);

        using var document = PdfDocument.Open(pdf);
        var text = PageText(Assert.Single(document.GetPages()));
        Assert.Contains("keine Schächte zugeordnet", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht erfasst", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht mehr vorhanden", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Viele_Schaechte_erhalten_auf_jeder_Folgeseite_Kopf_und_Tabellenkopf()
    {
        var lines = Enumerable.Range(1, 60)
            .Select(index => Line(
                "S-" + index.ToString("000", CultureInfo.InvariantCulture),
                "Strasse " + index.ToString(CultureInfo.InvariantCulture),
                index % 2 == 0 ? "Kontrollschacht" : "Einlaufschacht",
                (index % 5).ToString(CultureInfo.InvariantCulture)))
            .ToList();
        var model = new DossierShaftListPdfModel(
            "Mehrseitig",
            "Eigentümer",
            "Musterweg 1, 6460 Altdorf",
            new DateTime(2026, 8, 28),
            lines,
            MissingShaftCount: 0);

        var pdf = ServiceWithoutAssets().CreatePdf(model);

        using var document = PdfDocument.Open(pdf);
        Assert.True(document.NumberOfPages > 1);
        var pagesWithRows = document.GetPages()
            .Select(PageText)
            .Where(text => text.Contains("S-", StringComparison.Ordinal))
            .ToList();
        Assert.True(pagesWithRows.Count > 1);
        Assert.All(pagesWithRows, text =>
        {
            Assert.Contains("SCHACHTLISTE", text, StringComparison.Ordinal);
            Assert.Contains("Schacht", text, StringComparison.Ordinal);
            Assert.Contains("Strasse", text, StringComparison.Ordinal);
            Assert.Contains("Funktion", text, StringComparison.Ordinal);
            Assert.Contains("Zustand", text, StringComparison.Ordinal);
        });

        var allText = string.Join(" ", document.GetPages().Select(PageText));
        foreach (var line in lines)
            Assert.Equal(1, CountOccurrences(allText, line.Number));
    }

    private static SchachtRecord Shaft(
        string number,
        string street,
        string function,
        string condition)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", number);
        record.SetFieldValue(FieldKeys.Street, street);
        record.SetFieldValue("Funktion", function);
        record.SetFieldValue(FieldKeys.ConditionClass, condition);
        return record;
    }

    private static DossierShaftListPdfService ServiceWithoutAssets()
        => new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

    private static DossierShaftLine Line(
        string number,
        string street = "",
        string function = "",
        string condition = "")
        => new(Guid.NewGuid(), number, street, condition, 0m, function);

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
