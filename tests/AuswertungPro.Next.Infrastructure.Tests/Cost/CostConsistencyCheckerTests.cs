using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Charakterisierungs-Tests fuer CostConsistencyChecker (KK01–KK14).
/// Jede Regel wird durch mindestens einen positiven (Warnung ausgeloest)
/// und einen negativen (keine Warnung) Fall abgedeckt.
/// Schwellen-Grenzfaelle: 50 % (KK06) und 10 % (KK10) exakt.
/// </summary>
public sealed class CostConsistencyCheckerTests
{
    // ---------------------------------------------------------------------------
    // Test-Hilfsmethoden
    // ---------------------------------------------------------------------------

    private static CostConsistencyChecker Checker() => new();

    private static IReadOnlyDictionary<string, CostCatalogItem> EmptyCatalog()
        => new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, MeasureTemplate> EmptyTemplates()
        => new Dictionary<string, MeasureTemplate>(System.StringComparer.OrdinalIgnoreCase);

    private static void AssertRuleIds(IEnumerable<ConsistencyWarning> warnings, params string[] expectedRuleIds)
    {
        var actual = warnings
            .Select(w => w.RuleId)
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToArray();
        var expected = expectedRuleIds
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>Erstellt einen minimalen Block mit einer Zeile.</summary>
    private static MeasureBlockView OneLineBlock(
        ICostLineView line,
        string measureId = "M1",
        string measureName = "Testmassnahme",
        string? dnText = null,
        string? lengthText = null,
        string? connectionsText = null)
        => new()
        {
            MeasureId = measureId,
            MeasureName = measureName,
            DnText = dnText,
            LengthText = lengthText,
            ConnectionsText = connectionsText,
            Total = line.Selected ? line.Qty * line.UnitPrice : 0m,
            Lines = new[] { line }
        };

    private static CostLineView Line(
        string itemKey = "ITEM_A",
        string text = "Testposition",
        string unit = "m",
        decimal qty = 1m,
        decimal unitPrice = 100m,
        bool selected = true,
        bool priceMissing = false,
        bool isPriceOverridden = false)
        => new()
        {
            ItemKey = itemKey,
            Text = text,
            Unit = unit,
            Qty = qty,
            UnitPrice = unitPrice,
            Selected = selected,
            PriceMissing = priceMissing,
            IsPriceOverridden = isPriceOverridden
        };

    private static CostCatalogItem FixedItem(string key, decimal price)
        => new() { Key = key, Name = key, Type = "Fixed", Price = price };

    private static CostCatalogItem ByDnItem(string key, int dnFrom, int dnTo, decimal price)
        => new()
        {
            Key = key,
            Name = key,
            Type = "ByDN",
            DnPrices = new List<DnPrice>
            {
                new() { DnFrom = dnFrom, DnTo = dnTo, Price = price }
            }
        };

    private static CostCatalogItem ByDnItemWithQtySteps(string key, int dnFrom, int dnTo)
        => new()
        {
            Key = key,
            Name = key,
            Type = "ByDN",
            DnPrices = new List<DnPrice>
            {
                new() { DnFrom = dnFrom, DnTo = dnTo, QtyFrom = 0m, QtyTo = 50m, Price = 120m },
                new() { DnFrom = dnFrom, DnTo = dnTo, QtyFrom = 50.01m, Price = 90m }
            }
        };

    // ---------------------------------------------------------------------------
    // KK01: Preis 0, obwohl Katalog einen Preis hat
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK01_Ausg_Preis0_UndKatalogHatPreis_ErzeugtFehler()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 120m)
        };
        var block = OneLineBlock(Line(unitPrice: 0m, priceMissing: false));

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK01" && w.Severity == ConsistencyWarningSeverity.Error);
    }

    [Fact]
    public void KK01_Neg_Preis0_KatalogLeer_KeinFehler()
    {
        var block = OneLineBlock(Line(unitPrice: 0m, priceMissing: false));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09", "KK14");
    }

    [Fact]
    public void KK01_Neg_PriceMissing_KeinKK01_SondernKK02()
    {
        // Wenn PriceMissing=true, greift KK02, nicht KK01.
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 120m)
        };
        var block = OneLineBlock(Line(unitPrice: 0m, priceMissing: true));

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK02", "KK09", "KK14");
    }

    // ---------------------------------------------------------------------------
    // KK02: Katalogpreis fehlt
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK02_Ausg_PriceMissing_ErzeugtFehler()
    {
        var block = OneLineBlock(Line(priceMissing: true));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK02" && w.Severity == ConsistencyWarningSeverity.Error);
    }

    [Fact]
    public void KK02_Neg_PriceVorhanden_KeinFehler()
    {
        var block = OneLineBlock(Line(priceMissing: false));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    // ---------------------------------------------------------------------------
    // KK03: Einheit fehlt
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK03_Ausg_EinheitLeer_ErzeugtWarnung()
    {
        var block = OneLineBlock(Line(unit: ""));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK03" && w.Severity == ConsistencyWarningSeverity.Warning);
    }

    [Fact]
    public void KK03_Neg_EinheitGesetzt_KeinWarnung()
    {
        var block = OneLineBlock(Line(unit: "m"));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    // ---------------------------------------------------------------------------
    // KK04: Menge 0
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK04_Ausg_Menge0_ErzeugtWarnung()
    {
        var block = OneLineBlock(Line(qty: 0m));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK04" && w.Severity == ConsistencyWarningSeverity.Warning);
    }

    [Fact]
    public void KK04_Neg_MengeGroesserNull_KeinWarnung()
    {
        var block = OneLineBlock(Line(qty: 2m));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    // ---------------------------------------------------------------------------
    // KK05: Vorlage definiert Preis, aktuelle Zeile hat 0
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK05_Ausg_VorlageHatPreis_ZeileHat0_ErzeugtWarnung()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 150m)
        };
        var template = new MeasureTemplate
        {
            Id = "M1",
            Name = "Massnahme 1",
            Lines = new List<MeasureLineTemplate>
            {
                new() { ItemKey = "ITEM_A", Enabled = true, DefaultQty = 1m }
            }
        };
        var templates = new Dictionary<string, MeasureTemplate>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M1"] = template
        };
        var block = OneLineBlock(Line(unitPrice: 0m, priceMissing: false));

        var result = Checker().CheckAll(new[] { block }, catalog, templates, null, null);

        Assert.Contains(result, w => w.RuleId == "KK05" && w.Severity == ConsistencyWarningSeverity.Warning);
    }

    [Fact]
    public void KK05_Neg_VorlageZeileDeaktiviert_KeinWarnung()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 150m)
        };
        var template = new MeasureTemplate
        {
            Id = "M1",
            Name = "Massnahme 1",
            Lines = new List<MeasureLineTemplate>
            {
                // Enabled=false → KK05 darf nicht ausgeloest werden
                new() { ItemKey = "ITEM_A", Enabled = false, DefaultQty = 1m }
            }
        };
        var templates = new Dictionary<string, MeasureTemplate>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["M1"] = template
        };
        var block = OneLineBlock(Line(unitPrice: 0m, priceMissing: false));

        var result = Checker().CheckAll(new[] { block }, catalog, templates, null, null);

        AssertRuleIds(result, "KK01", "KK09", "KK14");
    }

    // ---------------------------------------------------------------------------
    // KK06: Preisabweichung >50 % vom Katalogpreis (Schwellen-Grenzfaelle)
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK06_Ausg_AbweichungExaktUeber50Prozent_ErzeugtWarnung()
    {
        // Katalogpreis 100, Abweichung =51 % → 151 CHF
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 100m)
        };
        var block = OneLineBlock(Line(unitPrice: 151m));

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK06");
    }

    [Fact]
    public void KK06_Grenzfall_ExaktGleich50Prozent_KeinWarnung()
    {
        // Abweichung genau 50 % darf KEINE Warnung erzeugen (Schwelle: >50 %)
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 100m)
        };
        var block = OneLineBlock(Line(unitPrice: 150m)); // 50 % = exakt Schwelle, kein Trigger

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    [Fact]
    public void KK06_Neg_AbweichungUnterSchwelle_KeinWarnung()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = FixedItem("ITEM_A", 100m)
        };
        var block = OneLineBlock(Line(unitPrice: 110m)); // 10 % Abweichung

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    [Fact]
    public void KK06_ByDn_PreisAbweichungUeber50Prozent_ErzeugtWarnung()
    {
        // ByDN-Preis = 200, Benutzer-Preis = 320 → 60 % Abweichung
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_B"] = ByDnItem("ITEM_B", 200, 400, 200m)
        };
        var block = OneLineBlock(
            line: Line(itemKey: "ITEM_B", unit: "m", unitPrice: 320m),
            dnText: "300");

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK06");
    }

    [Fact]
    public void KK06_ByDn_Mengenstaffel_Korrekter_Staffelpreis_ErzeugtKeineAbweichung()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_B"] = ByDnItemWithQtySteps("ITEM_B", 200, 400)
        };
        var block = OneLineBlock(
            line: Line(itemKey: "ITEM_B", unit: "m", qty: 60m, unitPrice: 90m),
            dnText: "300",
            lengthText: "60");

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    // ---------------------------------------------------------------------------
    // KK07: Preis manuell ueberschrieben
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK07_Ausg_IsPriceOverridden_ErzeugtInfo()
    {
        var block = OneLineBlock(Line(isPriceOverridden: true));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK07" && w.Severity == ConsistencyWarningSeverity.Info);
    }

    [Fact]
    public void KK07_Neg_NichtUeberschrieben_KeinWarnung()
    {
        var block = OneLineBlock(Line(isPriceOverridden: false));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    // ---------------------------------------------------------------------------
    // KK08: DN fehlt, ByDN-Position vorhanden
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK08_Ausg_DnFehlt_UndByDnItemVorhanden_ErzeugtFehler()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = ByDnItem("ITEM_A", 200, 400, 100m)
        };
        var block = OneLineBlock(Line(), dnText: null);

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK08" && w.Severity == ConsistencyWarningSeverity.Error);
    }

    [Fact]
    public void KK08_Neg_DnGesetzt_KeinFehler()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["ITEM_A"] = ByDnItem("ITEM_A", 200, 400, 100m)
        };
        var block = OneLineBlock(Line(), dnText: "300");

        var result = Checker().CheckAll(new[] { block }, catalog, EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    // ---------------------------------------------------------------------------
    // KK09: Laenge fehlt, Meter-Position vorhanden
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK09_Ausg_LaengeFehlt_MeterEinheitVorhanden_ErzeugtFehler()
    {
        var block = OneLineBlock(Line(unit: "m"), lengthText: null);

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK09" && w.Severity == ConsistencyWarningSeverity.Error);
    }

    [Fact]
    public void KK09_Neg_LaengeGesetzt_KeinFehler()
    {
        var block = OneLineBlock(Line(unit: "m"), lengthText: "45.00");

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    [Fact]
    public void KK09_Neg_KeineMetrEinheit_KeinFehler()
    {
        var block = OneLineBlock(Line(unit: "Stk"), lengthText: null);

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    // ---------------------------------------------------------------------------
    // KK10: Cross-Haltung-Preisabweichung >10 % (Schwellen-Grenzfaelle)
    // ---------------------------------------------------------------------------

    private static ProjectCostStore BuildStore(params (string Holding, string ItemKey, decimal Price)[] entries)
    {
        var store = new ProjectCostStore();
        foreach (var (holding, itemKey, price) in entries)
        {
            if (!store.ByHolding.TryGetValue(holding, out var hc))
            {
                hc = new HoldingCost { Holding = holding };
                store.ByHolding[holding] = hc;
            }
            var measure = new MeasureCost { MeasureId = "M1", MeasureName = "Test" };
            measure.Lines.Add(new CostLine
            {
                ItemKey = itemKey,
                Text = "Testposition",
                UnitPrice = price,
                Selected = true
            });
            hc.Measures.Add(measure);
        }
        return store;
    }

    [Fact]
    public void KK10_Ausg_AbweichungUeber10Prozent_ErzeugtWarnung()
    {
        // Min=100, Max=112 → 12 % > 10 %
        var store = BuildStore(("H1", "ITEM_A", 100m), ("H2", "ITEM_A", 112m));

        var result = Checker().CheckAll(
            System.Array.Empty<IMeasureBlockView>(), EmptyCatalog(), EmptyTemplates(),
            store, "H1");

        Assert.Contains(result, w => w.RuleId == "KK10");
    }

    [Fact]
    public void KK10_Grenzfall_ExaktGleich10Prozent_KeinWarnung()
    {
        // Min=100, Max=110 → exakt 10 % = kein Trigger (Schwelle: >10 %)
        var store = BuildStore(("H1", "ITEM_A", 100m), ("H2", "ITEM_A", 110m));

        var result = Checker().CheckAll(
            System.Array.Empty<IMeasureBlockView>(), EmptyCatalog(), EmptyTemplates(),
            store, "H1");

        AssertRuleIds(result);
    }

    [Fact]
    public void KK10_Neg_NurEineHaltung_KeinWarnung()
    {
        var store = BuildStore(("H1", "ITEM_A", 100m));

        var result = Checker().CheckAll(
            System.Array.Empty<IMeasureBlockView>(), EmptyCatalog(), EmptyTemplates(),
            store, "H1");

        AssertRuleIds(result);
    }

    [Fact]
    public void KK10_Neg_OhneProjectStore_KeinWarnung()
    {
        var result = Checker().CheckAll(
            System.Array.Empty<IMeasureBlockView>(), EmptyCatalog(), EmptyTemplates(),
            null, "H1");

        AssertRuleIds(result);
    }

    // ---------------------------------------------------------------------------
    // KK11: Keine aktivierten Zeilen
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK11_Ausg_KeineAktivierteZeilen_ErzeugtWarnung()
    {
        var block = OneLineBlock(Line(selected: false));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK11" && w.Severity == ConsistencyWarningSeverity.Warning);
    }

    [Fact]
    public void KK11_Neg_MindestensEineAktiviert_KeinWarnung()
    {
        var block = OneLineBlock(Line(selected: true));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    // ---------------------------------------------------------------------------
    // KK12: "Kanalroboter" im Text aber falsche Einheit
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK12_Ausg_KanalroboterMitFalscherEinheit_ErzeugtWarnung()
    {
        var block = OneLineBlock(Line(text: "Kanalroboter Einsatz", unit: "m"));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK12" && w.Severity == ConsistencyWarningSeverity.Warning);
    }

    [Fact]
    public void KK12_Neg_KanalroboterMitStd_KeinWarnung()
    {
        var block = OneLineBlock(Line(text: "Kanalroboter Einsatz", unit: "Std"));

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    [Fact]
    public void KK12_Neg_KanalroboterMitH_KeinWarnung()
    {
        var block = OneLineBlock(Line(text: "kanalroboter", unit: "h")); // Gross-/Kleinschreibung

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    // ---------------------------------------------------------------------------
    // KK13: Anschluss-Zeilen vorhanden aber 0 Anschluesse (Info)
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK13_Ausg_AnschlussItemDeaktiviert_OhneConnectionsText_ErzeugtInfo()
    {
        // Zeile hat "ANSCHLUSS" im Key, ist aber NICHT aktiviert → KK13
        var block = new MeasureBlockView
        {
            MeasureId = "M1",
            MeasureName = "Massnahme",
            ConnectionsText = null, // leer
            Total = 0m,
            Lines = new[]
            {
                new CostLineView
                {
                    ItemKey = "ITEM_ANSCHLUSS",
                    Text = "Anschluss",
                    Unit = "Stk",
                    Qty = 0m,
                    UnitPrice = 50m,
                    Selected = false // deaktiviert
                }
            }
        };

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK13" && w.Severity == ConsistencyWarningSeverity.Info);
    }

    [Fact]
    public void KK13_Neg_AnschlussAktiviertMitConnectionsText_KeinWarnung()
    {
        var block = new MeasureBlockView
        {
            MeasureId = "M1",
            MeasureName = "Massnahme",
            ConnectionsText = "2",
            Total = 100m,
            Lines = new[]
            {
                new CostLineView
                {
                    ItemKey = "ITEM_ANSCHLUSS",
                    Text = "Anschluss",
                    Unit = "Stk",
                    Qty = 2m,
                    UnitPrice = 50m,
                    Selected = true
                }
            }
        };

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    // ---------------------------------------------------------------------------
    // KK14: Gesamtkosten 0 obwohl Massnahmen ausgewaehlt
    // ---------------------------------------------------------------------------

    [Fact]
    public void KK14_Ausg_GesamtNullObwohlAusgewaehlt_ErzeugtWarnung()
    {
        // Block mit 0-Gesamtsumme, aber aktivierter Zeile
        var block = new MeasureBlockView
        {
            MeasureId = "M1",
            MeasureName = "Massnahme",
            Total = 0m, // Gesamtsumme 0
            Lines = new[]
            {
                new CostLineView
                {
                    ItemKey = "ITEM_A",
                    Text = "Test",
                    Unit = "m",
                    Qty = 5m,
                    UnitPrice = 0m, // → Total bleibt 0
                    Selected = true,
                    PriceMissing = false
                }
            }
        };

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        Assert.Contains(result, w => w.RuleId == "KK14" && w.Severity == ConsistencyWarningSeverity.Warning);
    }

    [Fact]
    public void KK14_Neg_GesamtGroesserNull_KeinWarnung()
    {
        var block = new MeasureBlockView
        {
            MeasureId = "M1",
            MeasureName = "Massnahme",
            Total = 500m,
            Lines = new[]
            {
                new CostLineView
                {
                    ItemKey = "ITEM_A",
                    Text = "Test",
                    Unit = "m",
                    Qty = 5m,
                    UnitPrice = 100m,
                    Selected = true
                }
            }
        };

        var result = Checker().CheckAll(new[] { block }, EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result, "KK09");
    }

    [Fact]
    public void KK14_Neg_KeineMassnahmen_KeinWarnung()
    {
        var result = Checker().CheckAll(
            System.Array.Empty<IMeasureBlockView>(), EmptyCatalog(), EmptyTemplates(), null, null);

        AssertRuleIds(result);
    }

    // ---------------------------------------------------------------------------
    // Hilfsmethoden-Tests: ResolveCatalogPrice
    // ---------------------------------------------------------------------------

    [Fact]
    public void ResolveCatalogPrice_Fixed_GibtFestenPreisZurueck()
    {
        var item = FixedItem("X", 250m);
        var price = CostConsistencyChecker.ResolveCatalogPrice(item, null);
        Assert.Equal(250m, price);
    }

    [Fact]
    public void ResolveCatalogPrice_ByDn_TrifftKorrektesIntervall()
    {
        var item = ByDnItem("X", 200, 400, 180m);
        var price = CostConsistencyChecker.ResolveCatalogPrice(item, "300");
        Assert.Equal(180m, price);
    }

    [Fact]
    public void ResolveCatalogPrice_ByDn_DnAusserhalb_NutztNaechstenDnFallback()
    {
        var item = ByDnItem("X", 200, 400, 180m);
        var price = CostConsistencyChecker.ResolveCatalogPrice(item, "600");
        Assert.Equal(180m, price);
    }

    [Fact]
    public void ResolveCatalogPrice_ByDn_DnTextLeer_GibtNullZurueck()
    {
        var item = ByDnItem("X", 200, 400, 180m);
        var price = CostConsistencyChecker.ResolveCatalogPrice(item, null);
        Assert.Null(price);
    }

    [Fact]
    public void ResolveCatalogPrice_ByDn_Beruecksichtigt_Mengenstaffel()
    {
        var item = ByDnItemWithQtySteps("X", 200, 400);

        var price = CostConsistencyChecker.ResolveCatalogPrice(item, "300", qty: 60m);

        Assert.Equal(90m, price);
    }

    // ---------------------------------------------------------------------------
    // Hilfsmethoden-Tests: IsMeterUnit / IsConnectionLine
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("m", true)]
    [InlineData("M", true)]
    [InlineData("  m ", true)]
    [InlineData("Stk", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMeterUnit_KorrektErkennung(string? unit, bool expected)
    {
        Assert.Equal(expected, CostConsistencyChecker.IsMeterUnit(unit));
    }

    [Theory]
    [InlineData("ITEM_ANSCHLUSS_01", "anything", true)]
    [InlineData("ITEM_NORMAL", "Anschluss reparieren", true)]
    [InlineData("ITEM_NORMAL", "Kanalroboter", false)]
    [InlineData("", "", false)]
    public void IsConnectionLine_KorrektErkennung(string itemKey, string text, bool expected)
    {
        var line = new CostLineView { ItemKey = itemKey, Text = text };
        Assert.Equal(expected, CostConsistencyChecker.IsConnectionLine(line));
    }
}
