using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer MatrixMeasureOptionBuilder.
/// </summary>
public sealed class MatrixMeasureOptionBuilderTests
{
    [Fact]
    public void Build_erste_option_ist_immer_keine()
    {
        var options = MatrixMeasureOptionBuilder.Build(
            new[] { ("GFK", "Renovierung") },
            Templates(("GFK", "GFK-Liner", HauptLine("GFK", "m"))),
            Catalog(("GFK", "m")));

        Assert.Null(options[0].Id);
        Assert.Equal("— keine —", options[0].Name);
    }

    [Fact]
    public void Build_renovierung_vor_reparatur()
    {
        var measures = new[]
        {
            ("MANSCHETTE", "Reparatur"),
            ("GFK", "Renovierung"),
        };
        var templates = Templates(
            ("GFK", "GFK-Liner", HauptLine("GFK", "m")),
            ("MANSCHETTE", "Manschette", HauptLine("MANSCHETTE", "Stk")));
        var catalog = Catalog(("GFK", "m"), ("MANSCHETTE", "Stk"));

        var options = MatrixMeasureOptionBuilder.Build(measures, templates, catalog);

        // Index 0 = keine, dann Renovierung, dann Reparatur.
        Assert.Null(options[0].Id);
        Assert.Equal("Renovierung", options[1].Kategorie);
        Assert.Equal("Reparatur", options[2].Kategorie);
    }

    [Fact]
    public void Build_einheit_stk_setzt_manuelleMenge()
    {
        var options = MatrixMeasureOptionBuilder.Build(
            new[] { ("MANSCHETTE", "Reparatur") },
            Templates(("MANSCHETTE", "Manschette", HauptLine("MANSCHETTE", "Stk"))),
            Catalog(("MANSCHETTE", "Stk")));

        var opt = options.Single(o => o.Id == "MANSCHETTE");
        Assert.True(opt.ManuelleMenge);
    }

    [Fact]
    public void Build_einheit_m_setzt_manuelleMenge_false()
    {
        var options = MatrixMeasureOptionBuilder.Build(
            new[] { ("GFK", "Renovierung") },
            Templates(("GFK", "GFK-Liner", HauptLine("GFK", "m"))),
            Catalog(("GFK", "m")));

        var opt = options.Single(o => o.Id == "GFK");
        Assert.False(opt.ManuelleMenge);
    }

    [Fact]
    public void Build_einheit_h_setzt_manuelleMenge_fuer_roboter()
    {
        var options = MatrixMeasureOptionBuilder.Build(
            new[] { ("KANALROBOTER", "Reparatur") },
            Templates(("KANALROBOTER", "Kanalroboter", HauptLine("HAUPTARBEIT_HINDERNISSE_ROBOTER", "h"))),
            Catalog(("HAUPTARBEIT_HINDERNISSE_ROBOTER", "h")));

        var opt = options.Single(o => o.Id == "KANALROBOTER");
        Assert.True(opt.ManuelleMenge);
        // HauptItemKey weicht von MeasureId ab (Roboter-Spezialfall).
        Assert.Equal("HAUPTARBEIT_HINDERNISSE_ROBOTER", opt.HauptItemKey);
    }

    [Fact]
    public void Build_template_nicht_in_liste_wird_uebersprungen()
    {
        var options = MatrixMeasureOptionBuilder.Build(
            new[] { ("FEHLT", "Renovierung") },
            Templates(), // kein Template fuer FEHLT
            Catalog());

        // Nur die "keine"-Option.
        Assert.Single(options);
        Assert.Null(options[0].Id);
    }

    // --- Hilfsmethoden ---

    private static IReadOnlyDictionary<string, MeasureTemplate> Templates(
        params (string Id, string Name, MeasureLineTemplate Haupt)[] entries)
    {
        return entries.ToDictionary(
            e => e.Id,
            e => new MeasureTemplate
            {
                Id = e.Id,
                Name = e.Name,
                Lines = new List<MeasureLineTemplate> { e.Haupt }
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static MeasureLineTemplate HauptLine(string itemKey, string unit) => new MeasureLineTemplate
    {
        Group = "Hauptarbeit",
        ItemKey = itemKey,
        Enabled = true,
        DefaultQty = 1m,
    };

    private static IReadOnlyDictionary<string, CostCatalogItem> Catalog(
        params (string Key, string Unit)[] entries)
    {
        return entries.ToDictionary(
            e => e.Key,
            e => new CostCatalogItem { Key = e.Key, Name = e.Key, Unit = e.Unit, Active = true },
            StringComparer.OrdinalIgnoreCase);
    }
}
