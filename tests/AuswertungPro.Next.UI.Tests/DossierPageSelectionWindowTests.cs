using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;

using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPageSelectionWindowTests
{
    [Fact]
    public void Zustandsklassen_Erklaerblatt_wird_als_Pflichtblatt_erkannt()
    {
        var pdf = new DossierConditionClassPdfService(
            templateAssetFolder: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            .CreatePdf();

        var pflichtblaetter = DossierPageSelectionWindow.FindePflichtblaetter(pdf);

        Assert.Equal([1], pflichtblaetter.OrderBy(seite => seite));
    }

    [Fact]
    public void Normale_Seite_mit_gleicher_Ueberschrift_ist_kein_Pflichtblatt()
    {
        using var speicher = new MemoryStream();
        using (var bauer = new PdfDocumentBuilder(speicher))
        {
            var schrift = bauer.AddStandard14Font(Standard14Font.Helvetica);
            bauer.AddPage(595, 842).AddText(
                "Zustandsklassen Z0 bis Z4",
                12,
                new UglyToad.PdfPig.Core.PdfPoint(50, 700),
                schrift);
        }

        var pflichtblaetter = DossierPageSelectionWindow.FindePflichtblaetter(
            speicher.ToArray());

        Assert.Empty(pflichtblaetter);
    }

    [Fact]
    public void Jede_Seite_der_Haltungsliste_ist_ein_Pflichtblatt()
    {
        var pdf = new DossierHoldingListPdfService(templateAssetFolder: Path.GetTempPath())
            .CreatePdf(DossierHoldingListPdfModelBuilder.Build(
                Dossier(),
                Snapshot(holdings: 60, shafts: 0),
                new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Local)));

        var pflichtblaetter = DossierPageSelectionWindow.FindePflichtblaetter(pdf);

        Assert.True(pflichtblaetter.Count >= 2, "Der Test braucht eine mehrseitige Liste.");
        Assert.Equal(
            Enumerable.Range(1, pflichtblaetter.Count),
            pflichtblaetter.OrderBy(seite => seite));
    }

    [Fact]
    public void Jede_Seite_der_Schachtliste_ist_ein_Pflichtblatt()
    {
        var pdf = new DossierShaftListPdfService(templateAssetFolder: Path.GetTempPath())
            .CreatePdf(DossierShaftListPdfModelBuilder.Build(
                Dossier(),
                Snapshot(holdings: 0, shafts: 60),
                new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Local)));

        var pflichtblaetter = DossierPageSelectionWindow.FindePflichtblaetter(pdf);

        Assert.True(pflichtblaetter.Count >= 2, "Der Test braucht eine mehrseitige Liste.");
    }

    [Fact]
    public void Die_Beschriftung_nennt_das_jeweilige_Pflichtblatt()
    {
        Assert.Equal(
            "Blatt 3 · Haltungsliste (Pflichtblatt)",
            DossierPageSelectionWindow.BeschrifteBlatt(3, "Haltungsliste"));
        Assert.Equal(
            "Blatt 2 · Dossier-Erklärung (Pflichtblatt)",
            DossierPageSelectionWindow.BeschrifteBlatt(2, "Dossier-Erklärung"));
        Assert.Equal("Blatt 5", DossierPageSelectionWindow.BeschrifteBlatt(5, null));
    }

    private static DossierDefinition Dossier()
        => new()
        {
            Name = "Testliegenschaft",
            OwnerName = "Muster AG",
            Address = "Musterweg 1",
            PostalCode = "6460",
            Town = "Altdorf"
        };

    private static DossierSnapshot Snapshot(int holdings, int shafts)
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
