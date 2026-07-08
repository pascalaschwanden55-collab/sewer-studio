using System.Linq;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPreviewFactoryTests
{
    private static HaltungRecord Holding(double laenge, string dn, decimal kosten)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungslaenge_m", laenge.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, false);
        r.SetFieldValue("DN_mm", dn, FieldSource.Manual, false);
        r.SetFieldValue("Kosten", kosten.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, false);
        return r;
    }

    [Fact]
    public void FromProject_mappt_kennzahlen_und_metadaten()
    {
        var project = new Project { Name = "Zone 1.15", Description = "Test" };
        project.Metadata["Auftraggeber"] = "Abwasser Uri";
        project.Metadata["Gemeinde"] = "Altdorf";
        project.Metadata["Zone"] = "1.15";
        project.Data.Add(Holding(30, "DN300", 100m));
        project.Data.Add(Holding(20, "DN300", 50m));

        var preview = ProjectPreviewFactory.FromProject(project, @"D:\P\zone.json");

        Assert.Equal("Zone 1.15", preview.Name);
        Assert.Equal(@"D:\P\zone.json", preview.Path);
        Assert.Equal(2, preview.HoldingCount);
        Assert.Equal(50d, preview.TotalLengthMeters);
        Assert.Equal(150m, preview.TotalCost);
        Assert.Equal("Abwasser Uri", preview.Auftraggeber);
        Assert.Equal("Altdorf", preview.Gemeinde);
        Assert.Equal("1.15", preview.Zone);

        // Zustandsklassen werden 1:1 aus dem Builder durchgereicht (robust gegen Builder-Interna):
        var expected = DashboardStatisticsBuilder.Build(project.Data);
        Assert.Equal(expected.ConditionClasses.Count, preview.ConditionClasses.Count);

        // DN/Kosten: beide Haltungen sind DN300 -> genau EIN Bucket mit Count 2 und Kosten 150.
        Assert.Single(preview.DnCostGroups);
        Assert.Equal(2, preview.DnCostGroups[0].Count);
        Assert.Equal(150m, preview.DnCostGroups[0].Cost);
    }

    [Fact]
    public void FromProject_fehlende_metadaten_werden_leer()
    {
        var project = new Project { Name = "X" };
        project.Metadata.Remove("Bearbeiter");

        var preview = ProjectPreviewFactory.FromProject(project, "p.json");

        Assert.Equal(string.Empty, preview.Bearbeiter);
    }
}
