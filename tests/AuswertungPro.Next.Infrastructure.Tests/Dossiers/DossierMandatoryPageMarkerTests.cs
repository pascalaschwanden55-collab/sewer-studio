using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Erklaerblatt, Haltungsliste und Schachtliste sind automatisch erzeugte
/// Pflichtblaetter. Jede ihrer Seiten traegt eine eigene unsichtbare Marke,
/// damit die Seitenauswahl sie erkennt und nicht mit einer gleich betitelten
/// Kundenseite verwechselt.
/// </summary>
public sealed class DossierMandatoryPageMarkerTests
{
    [Fact]
    public void Marken_sind_voneinander_verschieden()
    {
        var marker = new[]
        {
            DossierMandatoryPageMarkers.ConditionClassExplanation,
            DossierMandatoryPageMarkers.HoldingList,
            DossierMandatoryPageMarkers.ShaftList
        };

        Assert.Equal(marker.Length, marker.Distinct(StringComparer.Ordinal).Count());
        Assert.All(marker, value => Assert.False(string.IsNullOrWhiteSpace(value)));
        Assert.Equal(
            DossierConditionClassDefinitions.PdfRequiredPageMarker,
            DossierMandatoryPageMarkers.ConditionClassExplanation);
    }

    [Theory]
    [InlineData("Dossier-Erklärung")]
    [InlineData("Haltungsliste")]
    [InlineData("Schachtliste")]
    public void Jede_Marke_hat_eine_eigene_Beschriftung(string expected)
    {
        var marker = expected switch
        {
            "Dossier-Erklärung" => DossierMandatoryPageMarkers.ConditionClassExplanation,
            "Haltungsliste" => DossierMandatoryPageMarkers.HoldingList,
            _ => DossierMandatoryPageMarkers.ShaftList
        };

        Assert.Equal(expected, DossierMandatoryPageMarkers.FindLabel("Blatt " + marker + " Ende"));
    }

    [Fact]
    public void Eine_gewoehnliche_Kundenseite_ist_kein_Pflichtblatt()
        => Assert.Null(DossierMandatoryPageMarkers.FindLabel(
            "Haltungsliste zum Eigentümerdossier Seite 1 von 2"));

    [Fact]
    public void Jede_Seite_der_Haltungsliste_traegt_die_Haltungslistenmarke()
    {
        var service = new DossierHoldingListPdfService(templateAssetFolder: Path.GetTempPath());

        var pdf = service.CreatePdf(DossierHoldingListPdfModelBuilder.Build(
            CreateDossier(),
            CreateSnapshot(holdings: 60, shafts: 0),
            new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Local)));

        AssertEveryPageCarries(pdf, DossierMandatoryPageMarkers.HoldingList);
    }

    [Fact]
    public void Jede_Seite_der_Schachtliste_traegt_die_Schachtlistenmarke()
    {
        var service = new DossierShaftListPdfService(templateAssetFolder: Path.GetTempPath());

        var pdf = service.CreatePdf(DossierShaftListPdfModelBuilder.Build(
            CreateDossier(),
            CreateSnapshot(holdings: 0, shafts: 60),
            new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Local)));

        AssertEveryPageCarries(pdf, DossierMandatoryPageMarkers.ShaftList);
    }

    private static void AssertEveryPageCarries(byte[] pdf, string marker)
    {
        using var document = PdfDocument.Open(pdf);
        Assert.True(document.NumberOfPages >= 2, "Der Test braucht eine mehrseitige Liste.");

        foreach (var page in document.GetPages())
        {
            var text = string.Join(" ", page.GetWords().Select(word => word.Text));
            Assert.Equal(marker, DossierMandatoryPageMarkers.FindLabel(text) switch
            {
                "Haltungsliste" => DossierMandatoryPageMarkers.HoldingList,
                "Schachtliste" => DossierMandatoryPageMarkers.ShaftList,
                "Dossier-Erklärung" => DossierMandatoryPageMarkers.ConditionClassExplanation,
                _ => "keine Marke auf Seite " + page.Number
            });
        }
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
                    Guid.NewGuid(), "H" + index, "Musterweg", 12.5, "2", 0m, ""))
                .ToList(),
            [],
            statistik,
            Enumerable.Range(1, shafts)
                .Select(index => new DossierShaftLine(
                    Guid.NewGuid(), "S" + index, "Musterweg", "3", 0m))
                .ToList(),
            []);
    }
}
