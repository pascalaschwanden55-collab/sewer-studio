using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Schacht-Zeilenbauer fuellt dieselbe <see cref="DruckcenterRowVm"/> wie der
/// Haltungs-Zeilenbauer, damit Filter, Statistik und PDF-Ausgabe unveraendert
/// weiterlaufen. Die Kosten kommen ausschliesslich aus schacht_costs.json.
/// </summary>
public sealed class BuilderPageSchachtRowBuilderTests
{
    [Fact]
    public void Build_liest_Nummer_Eigentuemer_mit_Umlaut_und_Kosten_aus_dem_Schachtstore()
    {
        var record = Schacht("S-12");
        // WinCan schreibt "Eigentümer" mit Umlaut, die Schacht-Seite "Eigentuemer" (ASCII).
        record.Fields["Eigentümer"] = " AWU ";
        record.Fields["Strasse"] = " Dorfstrasse ";

        var store = StoreMit("S-12", "Rahmen/Deckel ersetzen", qty: 1m, unitPrice: 850m);

        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [record],
            new Dictionary<string, string>(),
            store));

        Assert.Equal(DruckcenterRowKind.Schacht, row.Kind);
        Assert.Equal("S-12", row.Holding);
        Assert.Equal("AWU", row.Owner);
        Assert.Equal("Dorfstrasse", row.Street);
        Assert.Equal(850m, row.NetCost);
        Assert.True(row.HasDetailedCost);
        Assert.Equal("Positionsdetails", row.CostSource);
        Assert.Equal("Rahmen/Deckel ersetzen", row.MeasuresPreview);
    }

    [Fact]
    public void Build_findet_Schachtkosten_ohne_Beachtung_der_Grossschreibung()
    {
        var record = Schacht("s-12");
        var store = StoreMit("S-12", "Bankett sanieren", qty: 2m, unitPrice: 120m);

        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [record],
            new Dictionary<string, string>(),
            store));

        Assert.Equal(240m, row.NetCost);
        Assert.True(row.HasDetailedCost);
    }

    [Fact]
    public void Build_meldet_Schacht_ohne_Massnahme_als_kostenlos()
    {
        var record = Schacht("S-99");

        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [record],
            new Dictionary<string, string>(),
            new ProjectCostStore()));

        Assert.Equal(0m, row.NetCost);
        Assert.False(row.HasDetailedCost);
        Assert.False(row.HasMeasures);
        Assert.Equal("Keine Kosten", row.CostSource);
    }

    [Fact]
    public void Build_uebernimmt_den_Projekteigentuemer_wenn_der_Schacht_keinen_traegt()
    {
        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [Schacht("S-1")],
            new Dictionary<string, string> { [FieldKeys.Owner] = " AWU " },
            new ProjectCostStore()));

        Assert.Equal("AWU", row.Owner);
    }

    /// <summary>
    /// Ein Schacht hat kein Haltungsdossier. Bleibt <c>Record</c> leer, kann der
    /// Dossier-Druck gar nicht erst versehentlich eine leere Haltung ausgeben.
    /// </summary>
    [Fact]
    public void Build_haengt_keine_Haltung_an_die_Schachtzeile()
    {
        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [Schacht("S-1")],
            new Dictionary<string, string>(),
            new ProjectCostStore()));

        Assert.Null(row.Record);
    }

    [Fact]
    public void Build_sortiert_nach_Eigentuemer_und_Schachtnummer()
    {
        var b = Schacht("S-2");
        b.Fields["Eigentuemer"] = "AWU";
        var a = Schacht("S-1");
        a.Fields["Eigentuemer"] = "AWU";
        var privat = Schacht("S-3");
        privat.Fields["Eigentuemer"] = "Privat";

        var rows = BuilderPageSchachtRowBuilder.Build(
            [b, privat, a],
            new Dictionary<string, string>(),
            new ProjectCostStore());

        Assert.Equal(["S-1", "S-2", "S-3"], rows.Select(r => r.Holding));
    }

    /// <summary>
    /// Es gibt ZWEI Schacht-Kostenquellen: die Schacht-Matrix (schacht_costs.json) und den
    /// Massnahmen-Dialog der Schaechte-Seite (schacht_empfehlungen.json). Wer nur mit dem
    /// Dialog arbeitet, muss seine Kosten trotzdem sehen.
    /// </summary>
    [Fact]
    public void Build_nimmt_die_Empfehlungskosten_wenn_die_Matrix_nichts_hat()
    {
        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [Schacht("80551")],
            new Dictionary<string, string>(),
            new ProjectCostStore(),
            StoreMit("80551", "Empfohlene Massnahmen", qty: 1m, unitPrice: 1100m)));

        Assert.Equal(1100m, row.NetCost);
        Assert.True(row.HasMeasures);
        Assert.Equal("Empfohlene Massnahmen", row.MeasuresPreview);
        Assert.Equal("Schacht-Massnahmen", row.CostSource);
    }

    /// <summary>Die Matrix ist die genauere Quelle und darf nicht verdraengt werden.</summary>
    [Fact]
    public void Build_bevorzugt_die_Matrix_vor_den_Empfehlungen()
    {
        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [Schacht("80551")],
            new Dictionary<string, string>(),
            StoreMit("80551", "Bankett sanieren", qty: 1m, unitPrice: 300m),
            StoreMit("80551", "Empfohlene Massnahmen", qty: 1m, unitPrice: 1100m)));

        Assert.Equal(300m, row.NetCost);
        Assert.Equal("Positionsdetails", row.CostSource);
        Assert.Equal("Bankett sanieren", row.MeasuresPreview);
    }

    [Fact]
    public void Build_kommt_ohne_Empfehlungsspeicher_aus()
    {
        var row = Assert.Single(BuilderPageSchachtRowBuilder.Build(
            [Schacht("S-1")],
            new Dictionary<string, string>(),
            new ProjectCostStore()));

        Assert.Equal(0m, row.NetCost);
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = nummer;
        return record;
    }

    private static ProjectCostStore StoreMit(
        string schachtnummer,
        string measureName,
        decimal qty,
        decimal unitPrice)
        => new()
        {
            ByHolding = new Dictionary<string, HoldingCost>
            {
                [schachtnummer] = new HoldingCost
                {
                    Holding = schachtnummer,
                    Measures =
                    [
                        new MeasureCost
                        {
                            MeasureName = measureName,
                            Lines = [new CostLine { Selected = true, Qty = qty, UnitPrice = unitPrice }]
                        }
                    ]
                }
            }
        };
}
