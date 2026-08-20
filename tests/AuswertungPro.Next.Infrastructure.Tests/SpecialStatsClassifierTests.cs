using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using AuswertungPro.Next.Application.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer SpecialStatsClassifier.
/// Decken das IST-Verhalten ab, bevor die Klasse extrahiert wird.
/// </summary>
public sealed class SpecialStatsClassifierTests
{
    // ---------------------------------------------------------------------------
    // TryResolveSpecialStatsCategory
    // ---------------------------------------------------------------------------

    [Fact]
    public void TryResolve_GfkViaSchlauchlinerGfkKey_ReturnsInlinerGfk()
    {
        var line = new CostLine { ItemKey = "SCHLAUCHLINER_GFK", Text = "Schlauchliner GFK", Unit = "m" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.InlinerGfk, cat);
    }

    [Fact]
    public void TryResolve_GfkViaTextGfkPlusLiner_ReturnsInlinerGfk()
    {
        var line = new CostLine { ItemKey = "FOO", Text = "GFK Liner einbauen", Unit = "m" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.InlinerGfk, cat);
    }

    [Fact]
    public void TryResolve_NadelfilzViaKey_ReturnsInlinerNadelfilz()
    {
        var line = new CostLine { ItemKey = "SCHLAUCHLINER_NADELFILZ", Text = "", Unit = "m" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.InlinerNadelfilz, cat);
    }

    [Fact]
    public void TryResolve_NadelfilzPlusLinerText_ReturnsInlinerNadelfilz()
    {
        var line = new CostLine { ItemKey = "", Text = "Nadelfilz Liner", Unit = "m" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.InlinerNadelfilz, cat);
    }

    [Fact]
    public void TryResolve_LinerendmanschetteToken_ReturnsLinerendmanschette()
    {
        var line = new CostLine { ItemKey = "LINERENDMANSCHETTE", Text = "", Unit = "stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.Linerendmanschette, cat);
    }

    [Fact]
    public void TryResolve_LemToken_ReturnsLinerendmanschette()
    {
        var line = new CostLine { ItemKey = "", Text = "Linerende LEM", Unit = "stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.Linerendmanschette, cat);
    }

    [Fact]
    public void TryResolve_ManschetteToken_ReturnsManschette()
    {
        var line = new CostLine { ItemKey = "", Text = "Manschette einbauen", Unit = "stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.Manschette, cat);
    }

    [Fact]
    public void TryResolve_UnknownLine_ReturnsFalseAndNone()
    {
        var line = new CostLine { ItemKey = "ANSCHLUSS_EINBINDEN", Text = "Anschluss einbinden", Unit = "stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.False(ok);
        Assert.Equal(SpecialStatsCategory.None, cat);
    }

    [Fact]
    public void TryResolve_NullLine_ReturnsFalse()
    {
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(null!, out var cat);
        Assert.False(ok);
        Assert.Equal(SpecialStatsCategory.None, cat);
    }

    [Fact]
    public void TryResolve_LinerendmanschetteHasPriorityOverManschette()
    {
        // LINERENDMANSCHETTE enthaelt "MANSCHETTE" — LEM-Zweig muss zuerst greifen
        var line = new CostLine { ItemKey = "LINERENDMANSCHETTE", Text = "LEM Manschette", Unit = "stk" };
        SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.Equal(SpecialStatsCategory.Linerendmanschette, cat);
    }

    // ---------------------------------------------------------------------------
    // ContainsToken
    // ---------------------------------------------------------------------------

    [Fact]
    public void ContainsToken_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(SpecialStatsClassifier.ContainsToken("GFK Liner", "gfk"));
    }

    [Fact]
    public void ContainsToken_EmptyText_ReturnsFalse()
    {
        Assert.False(SpecialStatsClassifier.ContainsToken("", "GFK"));
    }

    [Fact]
    public void ContainsToken_EmptyToken_ReturnsFalse()
    {
        Assert.False(SpecialStatsClassifier.ContainsToken("GFK Liner", ""));
    }

    // ---------------------------------------------------------------------------
    // NormalizeUnit
    // ---------------------------------------------------------------------------

    [Fact]
    public void NormalizeUnit_Uppercase_ReturnsLowercase()
    {
        Assert.Equal("m", SpecialStatsClassifier.NormalizeUnit("M"));
    }

    [Fact]
    public void NormalizeUnit_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", SpecialStatsClassifier.NormalizeUnit(null));
        Assert.Equal("", SpecialStatsClassifier.NormalizeUnit(""));
        Assert.Equal("", SpecialStatsClassifier.NormalizeUnit("  "));
    }

    // ---------------------------------------------------------------------------
    // ResolveDisplayUnit
    // ---------------------------------------------------------------------------

    [Fact]
    public void ResolveDisplayUnit_NoUnits_ReturnsDefaultUnit()
    {
        var bucket = new SpecialStatsBucket { DefaultUnit = "m" };
        Assert.Equal("m", SpecialStatsClassifier.ResolveDisplayUnit(bucket));
    }

    [Fact]
    public void ResolveDisplayUnit_SingleUnit_ReturnsThatUnit()
    {
        var bucket = new SpecialStatsBucket { DefaultUnit = "m" };
        bucket.Units.Add("lm");
        Assert.Equal("lm", SpecialStatsClassifier.ResolveDisplayUnit(bucket));
    }

    [Fact]
    public void ResolveDisplayUnit_MultipleUnits_ReturnsVariabel()
    {
        var bucket = new SpecialStatsBucket { DefaultUnit = "m" };
        bucket.Units.Add("m");
        bucket.Units.Add("lm");
        Assert.Equal("variabel", SpecialStatsClassifier.ResolveDisplayUnit(bucket));
    }

    // ---------------------------------------------------------------------------
    // SpecialStatsConfigs
    // ---------------------------------------------------------------------------

    [Fact]
    public void SpecialStatsConfigs_HasFiveEntries()
    {
        // Seit 2026-08-20 zusaetzlich Kurzliner (Pointliner/Partliner).
        Assert.Equal(5, SpecialStatsClassifier.SpecialStatsConfigs.Length);
    }

    [Fact]
    public void SpecialStatsConfigs_ContainsExpectedCategories()
    {
        var cats = SpecialStatsClassifier.SpecialStatsConfigs.Select(c => c.Category).ToArray();
        Assert.Contains(SpecialStatsCategory.InlinerGfk, cats);
        Assert.Contains(SpecialStatsCategory.InlinerNadelfilz, cats);
        Assert.Contains(SpecialStatsCategory.Manschette, cats);
        Assert.Contains(SpecialStatsCategory.Linerendmanschette, cats);
        Assert.Contains(SpecialStatsCategory.Kurzliner, cats);
    }

    // ---------------------------------------------------------------------------
    // CreateSpecialStatsBuckets
    // ---------------------------------------------------------------------------

    [Fact]
    public void CreateSpecialStatsBuckets_ContainsAllCategories()
    {
        var dict = SpecialStatsClassifier.CreateSpecialStatsBuckets();
        Assert.True(dict.ContainsKey(SpecialStatsCategory.InlinerGfk));
        Assert.True(dict.ContainsKey(SpecialStatsCategory.InlinerNadelfilz));
        Assert.True(dict.ContainsKey(SpecialStatsCategory.Manschette));
        Assert.True(dict.ContainsKey(SpecialStatsCategory.Linerendmanschette));
    }

    [Fact]
    public void CreateSpecialStatsBuckets_DefaultUnitsMatch()
    {
        var dict = SpecialStatsClassifier.CreateSpecialStatsBuckets();
        Assert.Equal("m", dict[SpecialStatsCategory.InlinerGfk].DefaultUnit);
        Assert.Equal("stk", dict[SpecialStatsCategory.Manschette].DefaultUnit);
    }

    // ---------------------------------------------------------------------------
    // Kurzliner (Pointliner / Partliner) — bis 2026-08-20 in keiner Statistik gezaehlt
    // ---------------------------------------------------------------------------

    [Fact]
    public void TryResolve_KurzlinerViaKey_ReturnsKurzliner()
    {
        var line = new CostLine { ItemKey = "KURZLINER_PARTLINER", Text = "Kurzliner (Pointliner, Partliner)", Unit = "Stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.Kurzliner, cat);
    }

    [Fact]
    public void TryResolve_PointlinerText_ReturnsKurzliner()
    {
        var line = new CostLine { ItemKey = "FOO", Text = "Pointliner setzen", Unit = "Stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.Kurzliner, cat);
    }

    [Fact]
    public void TryResolve_KurzlinerAusGfk_bleibt_Kurzliner()
    {
        // Ein Kurzliner aus GFK ist ein Kurzliner (Stueck) und kein Inliner (Meter).
        var line = new CostLine { ItemKey = "FOO", Text = "Kurzliner GFK", Unit = "Stk" };
        var ok = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var cat);
        Assert.True(ok);
        Assert.Equal(SpecialStatsCategory.Kurzliner, cat);
    }

    [Fact]
    public void SpecialStatsConfigs_enthaelt_Kurzliner_in_Stueck()
    {
        var eintrag = Assert.Single(
            SpecialStatsClassifier.SpecialStatsConfigs,
            c => c.Category == SpecialStatsCategory.Kurzliner);

        Assert.Equal("Kurzliner", eintrag.Label);
        Assert.Equal("stk", eintrag.DefaultUnit);
    }
}
