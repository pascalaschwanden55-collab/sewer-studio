using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Welche Bauteillisten das Gesamt-PDF bekommt, entscheidet allein der
/// Dossierstand: ohne Haltungen keine Haltungsliste, ohne Schaechte keine
/// Schachtliste — nie ein leeres Blatt.
/// </summary>
public sealed class DossierComponentListPdfRendererTests
{
    [Fact]
    public void Dossier_mit_Haltungen_und_Schaechten_erhaelt_beide_Listen_in_fester_Reihenfolge()
    {
        var renderer = CreateRenderer();

        var lists = renderer.Render(
            CreateDossier(),
            CreateSnapshot(holdings: 2, shafts: 3));

        Assert.Equal(["Haltungsliste", "Schachtliste"], lists.Select(liste => liste.Label));
        Assert.Contains("Haltungsliste", ReadText(lists[0].Pdf), StringComparison.Ordinal);
        Assert.Contains("Schachtliste", ReadText(lists[1].Pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Dossier_ohne_Schaechte_erhaelt_nur_die_Haltungsliste()
    {
        var renderer = CreateRenderer();

        var lists = renderer.Render(
            CreateDossier(),
            CreateSnapshot(holdings: 1, shafts: 0));

        var single = Assert.Single(lists);
        Assert.Equal("Haltungsliste", single.Label);
        Assert.Contains("Haltungsliste", ReadText(single.Pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Dossier_ohne_Haltungen_erhaelt_nur_die_Schachtliste()
    {
        var renderer = CreateRenderer();

        var lists = renderer.Render(
            CreateDossier(),
            CreateSnapshot(holdings: 0, shafts: 1));

        var single = Assert.Single(lists);
        Assert.Equal("Schachtliste", single.Label);
        Assert.Contains("Schachtliste", ReadText(single.Pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Dossier_ohne_Bauteile_erhaelt_keine_Liste()
    {
        var renderer = CreateRenderer();

        var lists = renderer.Render(
            CreateDossier(),
            CreateSnapshot(holdings: 0, shafts: 0));

        Assert.Empty(lists);
    }

    private static DossierComponentListPdfRenderer CreateRenderer()
        => new(
            new DossierHoldingListPdfService(templateAssetFolder: Path.GetTempPath()),
            new DossierShaftListPdfService(templateAssetFolder: Path.GetTempPath()),
            () => new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Local));

    private static string ReadText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join(
            " ",
            document.GetPages().Select(page => page.Text));
    }

    private static DossierDefinition CreateDossier()
        => new()
        {
            Name = "Testliegenschaft",
            OwnerName = "Muster AG",
            Address = "Musterweg 1",
            PostalCode = "6460",
            Town = "Altdorf"
        };

    private static DossierSnapshot CreateSnapshot(int holdings, int shafts)
    {
        var verteilung = new ZustandVerteilung(Array.Empty<ZustandBucket>());
        var statistik = new DashboardStatistics(
            0, 0, 0, 0, verteilung, verteilung,
            Array.Empty<DashboardBucket>(), Array.Empty<DashboardCostBucket>(), 0, 0, 0, 0, 0);

        return new DossierSnapshot(
            Guid.NewGuid(),
            "Testliegenschaft",
            Enumerable.Range(1, holdings)
                .Select(index => new DossierHoldingLine(
                    Guid.NewGuid(),
                    "H" + index,
                    "Musterweg",
                    12.5,
                    "2",
                    0m,
                    ""))
                .ToList(),
            [],
            statistik,
            Enumerable.Range(1, shafts)
                .Select(index => new DossierShaftLine(
                    Guid.NewGuid(),
                    "S" + index,
                    "Musterweg",
                    "3",
                    0m))
                .ToList(),
            []);
    }
}
