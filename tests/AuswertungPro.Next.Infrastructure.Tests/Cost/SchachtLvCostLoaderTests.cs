using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

public sealed class SchachtLvCostLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;
    private string ProjectPath => Path.Combine(_dir, "Projekt.json");

    private static HoldingCost SchachtCost(string nummer) => new()
    {
        Holding = nummer,
        Total = 1500m,
        Measures = new List<MeasureCost>
        {
            new()
            {
                MeasureId = "SCHACHT_PAUSCHAL", MeasureName = "Schachtsanierung pauschal",
                Total = 1500m,
                Lines = new List<CostLine>
                {
                    new() { ItemKey = "SCHACHT_SANIERUNG_PAUSCHAL", Text = "pauschal", Unit = "St", Qty = 1m, UnitPrice = 1500m, Selected = true }
                }
            }
        }
    };

    [Fact]
    public void Fehlende_Datei_liefert_leere_Liste_ohne_Fehler()
    {
        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.Null(loadError);
        Assert.Empty(result);
    }

    [Fact]
    public void Defekte_Datei_meldet_loadError_und_liefert_leer()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "costs"));
        File.WriteAllText(Path.Combine(_dir, "costs", "schacht_costs.json"), "{ kaputt");

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.NotNull(loadError);
        Assert.Contains("schacht_costs.json", loadError);
        Assert.Empty(result);
    }

    [Fact]
    public void Gespeicherte_Schachtkosten_werden_geladen_leere_ausgefiltert()
    {
        var store = new ProjectCostStore();
        store.ByHolding["KS 60191"] = SchachtCost("KS 60191");
        store.ByHolding["KS leer"] = new HoldingCost { Holding = "KS leer", Total = 0m }; // ohne Measures -> raus
        Assert.True(new ProjectCostStoreRepository("schacht_costs.json").Save(ProjectPath, store, out _));

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.Null(loadError);
        var one = Assert.Single(result);
        Assert.Equal("KS 60191", one.Holding);
    }

    /// <summary>
    /// Die Kosten des Schacht-Massnahmen-Dialogs (schacht_empfehlungen.json) gehoeren
    /// ebenfalls ins LV. Sie tragen keinen Katalog-ItemKey und keine Einheit — im LV
    /// erscheinen sie darum als Stueckposition mit Gesamtpreis.
    /// </summary>
    [Fact]
    public void Massnahmen_aus_dem_Dialog_kommen_als_Stueckposition_ins_LV()
    {
        SpeichereEmpfehlung("80551", "Schachthals sanieren", 400m);

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.Null(loadError);
        var cost = Assert.Single(result);
        Assert.Equal("80551", cost.Holding);
        var line = Assert.Single(cost.Measures.SelectMany(m => m.Lines));
        Assert.Equal("Schachthals sanieren", line.Text);
        Assert.Equal("Stk", line.Unit);
        Assert.Equal(1m, line.Qty);
        Assert.Equal(400m, line.UnitPrice);
        Assert.True(line.Selected);
    }

    /// <summary>
    /// Steht ein Schacht in beiden Dateien, gilt die Matrix. Sonst stuende derselbe
    /// Schacht zweimal im Leistungsverzeichnis — der Betrag waere zu hoch.
    /// </summary>
    [Fact]
    public void Matrix_verdraengt_die_Dialog_Massnahme_desselben_Schachts()
    {
        var store = new ProjectCostStore();
        store.ByHolding["80551"] = SchachtCost("80551");
        Assert.True(new ProjectCostStoreRepository("schacht_costs.json").Save(ProjectPath, store, out _));
        SpeichereEmpfehlung("80551", "Schachthals sanieren", 400m);

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out _);

        var cost = Assert.Single(result);
        Assert.Equal(1500m, cost.Total);
        Assert.Equal("pauschal", Assert.Single(cost.Measures.SelectMany(m => m.Lines)).Text);
    }

    [Fact]
    public void Beide_Quellen_ergeben_zusammen_alle_Schaechte()
    {
        var store = new ProjectCostStore();
        store.ByHolding["80551"] = SchachtCost("80551");
        Assert.True(new ProjectCostStoreRepository("schacht_costs.json").Save(ProjectPath, store, out _));
        SpeichereEmpfehlung("80534", "Bankett sanieren", 500m);

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out _);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Holding == "80551");
        Assert.Contains(result, c => c.Holding == "80534");
    }

    /// <summary>Eine vorhandene Einheit wird nicht ueberschrieben.</summary>
    [Fact]
    public void Vorhandene_Einheit_der_Dialog_Massnahme_bleibt_erhalten()
    {
        SpeichereEmpfehlung("80551", "Kanal spuelen", 300m, unit: "m");

        var result = SchachtLvCostLoader.LoadForLv(ProjectPath, out _);

        var line = Assert.Single(Assert.Single(result).Measures.SelectMany(m => m.Lines));
        Assert.Equal("m", line.Unit);
    }

    [Fact]
    public void Defekte_Massnahmendatei_meldet_loadError()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "costs"));
        File.WriteAllText(Path.Combine(_dir, "costs", "schacht_empfehlungen.json"), "{ kaputt");

        SchachtLvCostLoader.LoadForLv(ProjectPath, out var loadError);

        Assert.NotNull(loadError);
        Assert.Contains("schacht_empfehlungen.json", loadError);
    }

    private void SpeichereEmpfehlung(string nummer, string text, decimal preis, string unit = "")
    {
        var store = new ProjectCostStoreRepository("schacht_empfehlungen.json").Load(ProjectPath, out _);
        store.ByHolding[nummer] = new HoldingCost
        {
            Holding = nummer,
            Total = preis,
            Measures = new List<MeasureCost>
            {
                new()
                {
                    MeasureId = "SCHACHT_EMPFEHLUNG",
                    MeasureName = "Empfohlene Massnahmen",
                    Total = preis,
                    Lines = new List<CostLine>
                    {
                        new() { ItemKey = "", Text = text, Unit = unit, Qty = 1m, UnitPrice = preis, Selected = true }
                    }
                }
            }
        };
        Assert.True(new ProjectCostStoreRepository("schacht_empfehlungen.json").Save(ProjectPath, store, out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // ── Unwirksame Matrix darf keine gueltige Empfehlung verdraengen ────────
    // Gesamtaudit 2026-08-18, F-01: "hat Massnahmen" wurde hier mit
    // Measures.Count > 0 entschieden, im uebrigen Kostencode dagegen mit
    // "mindestens eine ausgewaehlte Zeile mit positiver Menge". Ein
    // Matrixeintrag mit leerer oder abgewaehlter Zeile belegte den Schacht und
    // liess die Empfehlung wegfallen - im Leistungsverzeichnis fehlte die
    // Position dann ganz.

    private static HoldingCost UnwirksameMatrix(string nummer, bool selected, decimal qty) => new()
    {
        Holding = nummer,
        Total = 0m,
        Measures = new List<MeasureCost>
        {
            new()
            {
                MeasureId = "LEER", MeasureName = "ohne wirksame Zeile", Total = 0m,
                Lines = new List<CostLine>
                {
                    new() { ItemKey = "X", Text = "x", Unit = "St", Qty = qty, UnitPrice = 100m, Selected = selected }
                }
            }
        }
    };

    [Theory]
    [InlineData(false, 1)]   // abgewaehlte Zeile
    [InlineData(true, 0)]    // ausgewaehlt, aber ohne Menge
    public void Unwirksame_Matrix_verdraengt_die_Empfehlung_nicht(bool selected, int qty)
    {
        var matrix = new ProjectCostStore();
        matrix.ByHolding["KS1"] = UnwirksameMatrix("KS1", selected, qty);
        var empfehlungen = new ProjectCostStore();
        empfehlungen.ByHolding["KS1"] = SchachtCost("KS1");

        var result = SchachtLvCostLoader.Merge(matrix, empfehlungen);

        var eintrag = Assert.Single(result);
        Assert.Equal("KS1", eintrag.Holding);
        Assert.Equal(1500m, eintrag.Total);
    }

    [Fact]
    public void Massnahme_ganz_ohne_Zeilen_verdraengt_die_Empfehlung_nicht()
    {
        var matrix = new ProjectCostStore();
        matrix.ByHolding["KS1"] = new HoldingCost
        {
            Holding = "KS1",
            Measures = new List<MeasureCost> { new() { MeasureId = "LEER" } }
        };
        var empfehlungen = new ProjectCostStore();
        empfehlungen.ByHolding["KS1"] = SchachtCost("KS1");

        var result = SchachtLvCostLoader.Merge(matrix, empfehlungen);

        Assert.Equal(1500m, Assert.Single(result).Total);
    }

    [Fact]
    public void Wirksame_Matrix_bleibt_vorrangig_und_wird_nicht_doppelt_gezaehlt()
    {
        var matrix = new ProjectCostStore();
        matrix.ByHolding["KS1"] = SchachtCost("KS1");
        var empfehlungen = new ProjectCostStore();
        empfehlungen.ByHolding["KS1"] = SchachtCost("KS1");

        var result = SchachtLvCostLoader.Merge(matrix, empfehlungen);

        Assert.Single(result);
    }
}
