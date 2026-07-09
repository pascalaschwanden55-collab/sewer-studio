using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DashboardStatisticsBuilderTests
{
    [Fact]
    public void Build_zaehlt_haltungen_schaechte_zustand_kosten_und_fortschritt()
    {
        var project = new Project();
        var h1 = Holding("H1", "0", "300", "12.5", "Ja");
        h1.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                [
                    new ProtocolEntry { Code = "BAB01" },
                    new ProtocolEntry { Code = "BCA02" }
                ]
            }
        };
        var h2 = Holding("H2", "2", "400", "7,5", "Nein");
        var h3 = Holding("H3", "", "300", "5", "");
        project.Data.Add(h1);
        project.Data.Add(h2);
        project.Data.Add(h3);
        project.SchaechteData.Add(Schacht("S1", "1"));
        project.SchaechteData.Add(Schacht("S2", ""));

        var hCosts = new ProjectCostStore
        {
            ByHolding =
            {
                ["H1"] = Cost("H1", 1200m),
                ["H2"] = Cost("H2", 300m)
            }
        };
        var sCosts = new ProjectCostStore
        {
            ByHolding =
            {
                ["S1"] = Cost("S1", 450m)
            }
        };

        var stats = DashboardStatisticsBuilder.Build(project, hCosts, sCosts);

        Assert.Equal(3, stats.HoldingCount);
        Assert.Equal(2, stats.SchachtCount);
        Assert.Equal(25d, stats.TotalLengthMeters);
        Assert.Equal(1950m, stats.TotalCost);
        Assert.Equal(1, stats.SanierenHaltungen);
        Assert.Equal(3, stats.HaltungenGesamt);
        Assert.Equal(1, stats.SchaechteMitMassnahmen);
        Assert.Equal(2, stats.DringendCount);
        Assert.Equal(2, stats.OhneZustandCount);
        Assert.Contains(stats.Haltungen.Buckets, b => b.Key == "0" && b.Count == 1);
        Assert.Contains(stats.Haltungen.Buckets, b => b.Key == "ohne" && b.Label == "ZU" && b.Count == 1);
        Assert.Contains(stats.Schaechte.Buckets, b => b.Key == "1" && b.Count == 1);
        Assert.Contains(stats.Schaechte.Buckets, b => b.Key == "ohne" && b.Label == "ZU" && b.Count == 1);
        Assert.Contains(stats.TopSchaeden, b => b.Key == "BAB" && b.Label == "BAB (Riss)" && b.Count == 1);
        Assert.DoesNotContain(stats.TopSchaeden, b => b.Key == "BCA");
        Assert.Contains(stats.HaltungDnCosts, b => b.Key == "300" && b.Cost == 1200m);
    }

    [Fact]
    public void Build_top_schaeden_zeigt_klartext_und_filtert_nicht_schaeden()
    {
        var record = Holding("H1", "2", "300", "12.5", "Nein");
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                [
                    new ProtocolEntry { Code = "BAB01" },
                    new ProtocolEntry { Code = "BAF.A" },
                    new ProtocolEntry { Code = "BCA02" },
                    new ProtocolEntry { Code = "BCC" },
                    new ProtocolEntry { Code = "BCD" },
                    new ProtocolEntry { Code = "BCE" },
                    new ProtocolEntry { Code = "BDA" }
                ]
            }
        };

        var stats = DashboardStatisticsBuilder.Build([record]);

        Assert.Equal(["BAB", "BAF"], stats.TopSchaeden.Select(b => b.Key).OrderBy(k => k));
        Assert.Contains(stats.TopSchaeden, b => b.Key == "BAB" && b.Label == "BAB (Riss)");
        Assert.Contains(stats.TopSchaeden, b => b.Key == "BAF" && b.Label == "BAF (Oberflaeche)");
    }

    [Theory]
    [InlineData("", "ohne")]
    [InlineData(" ", "ohne")]
    [InlineData("2.4", "2")]
    [InlineData("2,6", "3")]
    [InlineData("5", "ohne")]
    [InlineData("abc", "ohne")]
    public void NormalizeZustandsklasse_liefert_0_bis_4_oder_ohne(string raw, string expected)
    {
        Assert.Equal(expected, DashboardStatisticsBuilder.NormalizeZustandsklasse(raw));
    }

    [Fact]
    public void Build_leeres_projekt_liefert_geordnete_null_buckets()
    {
        var stats = DashboardStatisticsBuilder.Build(new Project(), null, null);

        Assert.False(stats.HasData);
        Assert.Equal(0m, stats.TotalCost);
        Assert.Equal(["0", "1", "2", "3", "4", "ohne"], stats.Haltungen.Buckets.Select(b => b.Key));
        Assert.All(stats.Haltungen.Buckets, b => Assert.Equal(0, b.Count));
        Assert.All(stats.Schaechte.Buckets, b => Assert.Equal(0, b.Count));
    }

    private static HaltungRecord Holding(string name, string condition, string dn, string length, string sanieren)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Zustandsklasse", condition, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", length, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Sanieren_JaNein", sanieren, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Kosten", "999999", FieldSource.Manual, userEdited: false);
        return record;
    }

    private static SchachtRecord Schacht(string nummer, string condition)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer);
        record.SetFieldValue("Zustandsklasse", condition);
        return record;
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
