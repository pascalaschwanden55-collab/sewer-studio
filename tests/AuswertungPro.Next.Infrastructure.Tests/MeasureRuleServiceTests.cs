using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer MeasureRuleService.
/// </summary>
public sealed class MeasureRuleServiceTests
{
    // -------------------------------------------------------------------------
    // GetRequiredInstallationItemKey
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("GFK_INLINER", "GFK Inliner", "INSTALL_UV_ANLAGE")]
    [InlineData("GFK", "Massnahme GFK", "INSTALL_UV_ANLAGE")]
    [InlineData("NADELFILZ_X", "Nadelfilz Renovierung", "INSTALL_HL_ANLAGE")]
    [InlineData("KURZLINER", "Kurzliner", null)]
    [InlineData("", "", null)]
    public void GetRequiredInstallationItemKey_ReturnsCorrectKey(
        string measureId, string measureName, string? expected)
    {
        var result = MeasureRuleService.GetRequiredInstallationItemKey(measureId, measureName);
        Assert.Equal(expected, result);
    }

    // -------------------------------------------------------------------------
    // EnforceInstallationRule – Zeile fehlt, wird hinzugefuegt
    // -------------------------------------------------------------------------

    [Fact]
    public void EnforceInstallationRule_MissingLine_ReturnsLineToAdd()
    {
        var lines = new List<CostLine>(); // keine Installations-Zeile
        var catalog = Catalog(
            new CostCatalogItem
            {
                Key = "INSTALL_UV_ANLAGE",
                Name = "UV-Haerteanlage",
                Unit = "Stk",
                Type = "Fixed",
                Price = 500m,
                Active = true
            });

        MeasureRuleService.EnforceInstallationRule(
            lines, catalog, "INSTALL_UV_ANLAGE",
            out var remove, out var add, out var changed);

        Assert.True(changed);
        Assert.NotNull(add);
        Assert.Equal("INSTALL_UV_ANLAGE", add!.ItemKey);
        Assert.Equal(1m, add.Qty);
        Assert.True(add.Selected);
        Assert.Empty(remove);
    }

    [Fact]
    public void EnforceInstallationRule_CorrectLinePresent_NoChange()
    {
        var lines = new List<CostLine>
        {
            new() { ItemKey = "INSTALL_UV_ANLAGE", Group = "Installation", Qty = 1m, Selected = true }
        };
        var catalog = Catalog(
            new CostCatalogItem { Key = "INSTALL_UV_ANLAGE", Name = "UV", Unit = "Stk", Type = "Fixed", Active = true });

        MeasureRuleService.EnforceInstallationRule(
            lines, catalog, "INSTALL_UV_ANLAGE",
            out var remove, out var add, out var changed);

        Assert.False(changed);
        Assert.Null(add);
        Assert.Empty(remove);
    }

    [Fact]
    public void EnforceInstallationRule_WrongInstallLine_SchedulesRemoval()
    {
        var lines = new List<CostLine>
        {
            new() { ItemKey = "INSTALL_HL_ANLAGE", Group = "Installation", Qty = 1m, Selected = true }
        };
        var catalog = Catalog(
            new CostCatalogItem { Key = "INSTALL_UV_ANLAGE", Name = "UV", Unit = "Stk", Type = "Fixed", Active = true },
            new CostCatalogItem { Key = "INSTALL_HL_ANLAGE", Name = "HL", Unit = "Stk", Type = "Fixed", Active = true });

        MeasureRuleService.EnforceInstallationRule(
            lines, catalog, "INSTALL_UV_ANLAGE",
            out var remove, out var add, out var changed);

        Assert.True(changed);
        Assert.NotNull(add);                          // die richtige Zeile soll hinzugefuegt werden
        Assert.Single(remove);                        // falsche Zeile soll entfernt werden
        Assert.Equal("INSTALL_HL_ANLAGE", remove[0].ItemKey);
    }

    [Fact]
    public void EnforceInstallationRule_InactiveLineReactivated()
    {
        // Zeile vorhanden aber deaktiviert
        var installLine = new CostLine
        {
            ItemKey = "INSTALL_UV_ANLAGE",
            Group = "Installation",
            Qty = 1m,
            Selected = false
        };
        var lines = new List<CostLine> { installLine };
        var catalog = Catalog(
            new CostCatalogItem { Key = "INSTALL_UV_ANLAGE", Name = "UV", Unit = "Stk", Type = "Fixed", Active = true });

        MeasureRuleService.EnforceInstallationRule(
            lines, catalog, "INSTALL_UV_ANLAGE",
            out _, out _, out var changed);

        Assert.True(changed);
        Assert.True(installLine.Selected);
    }

    [Fact]
    public void EnforceInstallationRule_UnknownKey_NoChange()
    {
        // Schluessel fehlt im Katalog -> Regel nicht anwenden
        var lines = new List<CostLine>();
        var catalog = Catalog(); // leer

        MeasureRuleService.EnforceInstallationRule(
            lines, catalog, "INSTALL_UV_ANLAGE",
            out var remove, out var add, out var changed);

        Assert.False(changed);
        Assert.Null(add);
        Assert.Empty(remove);
    }

    // -------------------------------------------------------------------------
    // EnforceEndManschetteRule
    // -------------------------------------------------------------------------

    [Fact]
    public void EnforceEndManschetteRule_AboveDn200_ActivatesLineWithDefaultQty()
    {
        var lemLine = new CostLine
        {
            ItemKey = "LINERENDMANSCHETTE_LEM",
            Selected = false,
            Qty = 0m,
            IsQtyOverridden = false
        };
        var lines = new List<CostLine> { lemLine };

        MeasureRuleService.EnforceEndManschetteRule(lines, dn: 250, out var changed);

        Assert.True(changed);
        Assert.True(lemLine.Selected);
        Assert.Equal(2m, lemLine.Qty); // Standardmenge = 2 (Anfang + Ende)
    }

    [Fact]
    public void EnforceEndManschetteRule_BelowDn200_DeactivatesLine()
    {
        var lemLine = new CostLine
        {
            ItemKey = "LINERENDMANSCHETTE_LEM",
            Selected = true,
            Qty = 2m,
            IsQtyOverridden = false
        };
        var lines = new List<CostLine> { lemLine };

        MeasureRuleService.EnforceEndManschetteRule(lines, dn: 150, out var changed);

        Assert.True(changed);
        Assert.False(lemLine.Selected);
        Assert.Equal(0m, lemLine.Qty);
    }

    [Fact]
    public void EnforceEndManschetteRule_ExactlyDn200_Activates()
    {
        var lemLine = new CostLine
        {
            ItemKey = "LINERENDMANSCHETTE_LEM",
            Selected = false,
            Qty = 0m,
            IsQtyOverridden = false
        };
        var lines = new List<CostLine> { lemLine };

        MeasureRuleService.EnforceEndManschetteRule(lines, dn: 200, out var changed);

        Assert.True(changed);
        Assert.True(lemLine.Selected);
    }

    [Fact]
    public void EnforceEndManschetteRule_NullDn_NoChange()
    {
        var lemLine = new CostLine
        {
            ItemKey = "LINERENDMANSCHETTE_LEM",
            Selected = true,
            Qty = 2m
        };
        var lines = new List<CostLine> { lemLine };

        MeasureRuleService.EnforceEndManschetteRule(lines, dn: null, out var changed);

        Assert.False(changed);
        Assert.True(lemLine.Selected);
        Assert.Equal(2m, lemLine.Qty);
    }

    [Fact]
    public void EnforceEndManschetteRule_QtyOverridden_AboveDn200_NotResetToDefault()
    {
        // Menge wurde manuell auf 3 gesetzt -> soll behalten werden
        var lemLine = new CostLine
        {
            ItemKey = "LINERENDMANSCHETTE_LEM",
            Selected = true,
            Qty = 3m,
            IsQtyOverridden = true
        };
        var lines = new List<CostLine> { lemLine };

        MeasureRuleService.EnforceEndManschetteRule(lines, dn: 300, out var changed);

        Assert.False(changed);
        Assert.Equal(3m, lemLine.Qty);
    }

    [Fact]
    public void EnforceEndManschetteRule_NoLemLines_NoChange()
    {
        var lines = new List<CostLine>
        {
            new() { ItemKey = "SCHLAUCHLINER_A", Qty = 50m, Selected = true }
        };

        MeasureRuleService.EnforceEndManschetteRule(lines, dn: 300, out var changed);

        Assert.False(changed);
    }

    // -------------------------------------------------------------------------
    // Hilfsfunktionen
    // -------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, CostCatalogItem> Catalog(
        params CostCatalogItem[] items)
        => items.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
}
