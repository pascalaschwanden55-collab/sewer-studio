using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPreviewFactoryTests
{
    [Fact]
    public void FromProject_mappt_kennzahlen_und_metadaten()
    {
        var project = new Project { Name = "Zone 1.15", Description = "Test" };
        project.Metadata["Auftraggeber"] = "Abwasser Uri";
        project.Metadata["Gemeinde"] = "Altdorf";
        project.Metadata["Zone"] = "1.15";
        project.Data.Add(Holding("H1", 30, "300"));
        project.SchaechteData.Add(Schacht("S1"));
        var hCosts = new ProjectCostStore { ByHolding = { ["H1"] = Cost("H1", 500m) } };
        var sCosts = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 100m) } };

        var preview = ProjectPreviewFactory.FromProject(project, @"D:\P\zone.json", hCosts, sCosts);

        Assert.Equal("Zone 1.15", preview.Name);
        Assert.Equal(@"D:\P\zone.json", preview.Path);
        Assert.Equal(1, preview.HoldingCount);
        Assert.Equal(1, preview.SchachtCount);
        Assert.Equal(30d, preview.TotalLengthMeters);
        Assert.Equal(600m, preview.TotalCost);
        Assert.Equal(600m, preview.Statistics.TotalCost);
        Assert.Equal("Abwasser Uri", preview.Auftraggeber);
        Assert.Equal("Altdorf", preview.Gemeinde);
        Assert.Equal("1.15", preview.Zone);

        Assert.Equal(preview.Statistics.Haltungen.Buckets.Count, preview.ConditionClasses.Count);
        Assert.Single(preview.DnCostGroups);
        Assert.Equal(1, preview.DnCostGroups[0].Count);
        Assert.Equal(500m, preview.DnCostGroups[0].Cost);
    }

    [Fact]
    public void FromProject_fehlende_metadaten_werden_leer()
    {
        var project = new Project { Name = "X" };
        project.Metadata.Remove("Bearbeiter");

        var preview = ProjectPreviewFactory.FromProject(project, "p.json");

        Assert.Equal(string.Empty, preview.Bearbeiter);
    }

    private static HaltungRecord Holding(string name, double laenge, string dn)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        r.SetFieldValue("Haltungslaenge_m", laenge.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, false);
        r.SetFieldValue("DN_mm", dn, FieldSource.Manual, false);
        return r;
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var r = new SchachtRecord();
        r.SetFieldValue("Schachtnummer", nummer);
        return r;
    }

    private static HoldingCost Cost(string key, decimal total)
        => new()
        {
            Holding = key,
            Total = total,
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = "M",
                    MeasureName = "Massnahme",
                    Total = total,
                    Lines =
                    [
                        new CostLine
                        {
                            ItemKey = "M",
                            Text = "Massnahme",
                            Qty = 1m,
                            UnitPrice = total,
                            Selected = true
                        }
                    ]
                }
            ]
        };
}
