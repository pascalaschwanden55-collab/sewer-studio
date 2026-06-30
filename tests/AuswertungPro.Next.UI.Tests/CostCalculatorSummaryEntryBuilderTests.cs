using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorSummaryEntryBuilderTests
{
    [Fact]
    public void BuildOwnerLookup_sammelt_eigentuemer_und_haltung_record_ueberschreibt_projektliste()
    {
        var projectRecords = new[]
        {
            Record(" H-1 ", "Gemeinde A"),
            Record("H-2", ""),
            Record("", "Gemeinde ohne Haltung")
        };
        var currentRecord = Record("H-1", "Gemeinde B");

        var lookup = CostCalculatorSummaryEntryBuilder.BuildOwnerLookup(projectRecords, currentRecord);

        Assert.Equal("Gemeinde B", lookup["H-1"]);
        Assert.False(lookup.ContainsKey("H-2"));
        Assert.False(lookup.ContainsKey(""));
    }

    [Fact]
    public void Build_gibt_entry_fuer_haltung_mit_selektierten_kostenlinien_zurueck()
    {
        var cost = Cost(" H-1 ", selected: true);
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["H-1"] = " Gemeinde A "
        };

        var entry = Assert.Single(CostCalculatorSummaryEntryBuilder.Build(cost, owners));

        Assert.Equal("H-1", entry.Holding);
        Assert.Equal("Gemeinde A", entry.Owner);
        Assert.Same(cost, entry.Cost);
    }

    [Fact]
    public void Build_liefert_leer_ohne_haltung_oder_ohne_selektierte_kostenlinien()
    {
        Assert.Empty(CostCalculatorSummaryEntryBuilder.Build(Cost("", selected: true), new Dictionary<string, string>()));
        Assert.Empty(CostCalculatorSummaryEntryBuilder.Build(Cost("H-1", selected: false), new Dictionary<string, string>()));
    }

    [Fact]
    public void Build_nutzt_unbekannt_wenn_eigentuemer_fehlt()
    {
        var entry = Assert.Single(CostCalculatorSummaryEntryBuilder.Build(
            Cost("H-1", selected: true),
            new Dictionary<string, string>()));

        Assert.Equal("Unbekannt", entry.Owner);
    }

    private static HaltungRecord Record(string holding, string owner)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Eigentuemer", owner, FieldSource.Manual, userEdited: true);
        return record;
    }

    private static HoldingCost Cost(string holding, bool selected)
        => new()
        {
            Holding = holding,
            Measures =
            {
                new MeasureCost
                {
                    Lines =
                    {
                        new CostLine { Selected = selected }
                    }
                }
            }
        };
}
