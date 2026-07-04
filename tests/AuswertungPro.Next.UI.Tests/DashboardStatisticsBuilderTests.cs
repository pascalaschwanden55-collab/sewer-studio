using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DashboardStatisticsBuilderTests
{
    [Fact]
    public void Build_groups_conditions_damage_codes_dn_and_costs()
    {
        var first = CreateRecord("1", "250", "12.5", "1000.50");
        first.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                [
                    new ProtocolEntry { Code = "BCA01" },
                    new ProtocolEntry { Code = "BCA02" },
                    new ProtocolEntry { Code = "BAB" }
                ]
            }
        };

        var second = CreateRecord("3", "400", "7,5", "499.50");
        second.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BBA10" });

        var stats = DashboardStatisticsBuilder.Build([first, second]);

        Assert.Equal(2, stats.TotalHoldings);
        Assert.Equal(20d, stats.TotalLengthMeters);
        Assert.Equal(1500m, stats.TotalCost);

        Assert.Contains(stats.ConditionClasses, b => b.Label == "1" && b.Count == 1 && b.Percent == 50d);
        Assert.Contains(stats.ConditionClasses, b => b.Label == "3" && b.Count == 1 && b.Percent == 50d);

        Assert.Contains(stats.DamageGroups, b => b.Label == "BCA" && b.Count == 2);
        Assert.Contains(stats.DamageGroups, b => b.Label == "BAB" && b.Count == 1);
        Assert.Contains(stats.DamageGroups, b => b.Label == "BBA" && b.Count == 1);

        Assert.Contains(stats.DnCostGroups, b => b.Label == "DN 250" && b.Count == 1 && b.Cost == 1000.50m);
        Assert.Contains(stats.DnCostGroups, b => b.Label == "DN 400" && b.Count == 1 && b.Cost == 499.50m);
    }

    private static HaltungRecord CreateRecord(string condition, string dn, string length, string cost)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Zustandsklasse", condition, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", length, FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Kosten", cost, FieldSource.Xtf, userEdited: false);
        return record;
    }
}
