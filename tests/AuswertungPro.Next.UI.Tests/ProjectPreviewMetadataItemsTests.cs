using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPreviewMetadataItemsTests
{
    [Fact]
    public void Build_blendet_leere_metadaten_aus_und_trimmed_werte()
    {
        var preview = new ProjectPreview(
            Name: "P",
            Description: "",
            Path: "p.json",
            ModifiedAtUtc: null,
            HoldingCount: 0,
            SchachtCount: 0,
            TotalLengthMeters: 0,
            TotalCost: 0m,
            Auftraggeber: "  Kanton Uri  ",
            Gemeinde: "",
            Zone: "1.15",
            Strasse: " ",
            Bearbeiter: "Pascal",
            Inspektionsdatum: "",
            AuftragNr: "A-42",
            Firma: "",
            Statistics: DashboardStatisticsBuilder.Build(new Project(), null, null));

        var items = ProjectPreviewMetadataItems.Build(preview);

        Assert.Equal(["Auftraggeber", "Zone", "Bearbeiter", "Auftrag-Nr"], items.Select(i => i.Label));
        Assert.Equal("Kanton Uri", items[0].Value);
    }

    [Fact]
    public void Build_null_liefert_leere_liste()
    {
        Assert.Empty(ProjectPreviewMetadataItems.Build(null));
    }
}
