using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageRowFilterTests
{
    [Fact]
    public void Apply_sortiert_gefilterte_zeilen_wie_druckcenter()
    {
        var rows = new[]
        {
            Row("H-3", "Zeta", ""),
            Row("H-2", "Beta", "Baumeister"),
            Row("H-1", "Alpha", "Baumeister")
        };

        var filtered = BuilderPageRowFilter.Apply(rows, EmptyCriteria());

        Assert.Equal(new[] { "H-1", "H-2", "H-3" }, filtered.Select(row => row.Holding));
    }

    [Fact]
    public void Apply_filtert_combo_werte_case_insensitive()
    {
        var rows = new[]
        {
            Row("H-1", "Gemeinde", "Kanalsanierer", sanieren: "Ja", material: "Beton", status: "Offen", year: "2026"),
            Row("H-2", "Privat", "Baumeister", sanieren: "Nein", material: "PVC", status: "Abgeschlossen", year: "2025")
        };

        var filtered = BuilderPageRowFilter.Apply(
            rows,
            EmptyCriteria() with
            {
                Owner = "gemeinde",
                ExecutedBy = "kanalsanierer",
                Sanieren = "ja",
                Material = "beton",
                Status = "offen",
                Year = "2026"
            });

        Assert.Single(filtered);
        Assert.Equal("H-1", filtered[0].Holding);
    }

    [Fact]
    public void Apply_sucht_in_druckcenter_textfeldern()
    {
        var rows = new[]
        {
            Row("H-1", "Gemeinde", "Kanalsanierer", street: "Dorfstrasse", measuresPreview: "Inliner GFK"),
            Row("H-2", "Privat", "Baumeister", street: "Hauptstrasse", measuresPreview: "Manschette")
        };

        var filtered = BuilderPageRowFilter.Apply(rows, EmptyCriteria() with { Search = "gfk" });

        Assert.Single(filtered);
        Assert.Equal("H-1", filtered[0].Holding);
    }

    [Fact]
    public void Apply_filtert_nur_mit_kosten_und_massnahmen()
    {
        var rows = new[]
        {
            Row("H-1", "Gemeinde", "Kanalsanierer", netCost: 120m, hasMeasures: true),
            Row("H-2", "Gemeinde", "Kanalsanierer", netCost: 0m, hasMeasures: true),
            Row("H-3", "Gemeinde", "Kanalsanierer", netCost: 80m, hasMeasures: false)
        };

        var filtered = BuilderPageRowFilter.Apply(rows, EmptyCriteria() with
        {
            OnlyWithCost = true,
            OnlyWithMeasures = true
        });

        Assert.Single(filtered);
        Assert.Equal("H-1", filtered[0].Holding);
    }

    private static BuilderPageFilterCriteria EmptyCriteria()
        => new(
            Owner: BuilderPageRowFilter.AllFilterLabel,
            ExecutedBy: BuilderPageRowFilter.AllFilterLabel,
            Sanieren: BuilderPageRowFilter.AllFilterLabel,
            Material: BuilderPageRowFilter.AllFilterLabel,
            Status: BuilderPageRowFilter.AllFilterLabel,
            Year: BuilderPageRowFilter.AllFilterLabel,
            Search: "",
            OnlyWithCost: false,
            OnlyWithMeasures: false);

    private static DruckcenterRowVm Row(
        string holding,
        string owner,
        string executedBy,
        string sanieren = "",
        string material = "",
        string status = "",
        string year = "",
        string street = "",
        string zustand = "",
        string measuresPreview = "",
        decimal netCost = 0m,
        bool hasMeasures = false)
        => new()
        {
            Holding = holding,
            Owner = owner,
            ExecutedBy = executedBy,
            Sanieren = sanieren,
            Material = material,
            Status = status,
            Year = year,
            Street = street,
            Zustand = zustand,
            MeasuresPreview = measuresPreview,
            NetCost = netCost,
            HasMeasures = hasMeasures
        };

}
