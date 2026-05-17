using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MaterialPlausibilityFilterTests
{
    private static RawVideoDetection Det(string code, double meter)
        => new(
            FindingLabel: code,
            MeterStart: meter,
            MeterEnd: meter,
            Severity: "mid",
            VsaCodeHint: code);

    // ── IsKunststoff ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("PE")]
    [InlineData("HDPE")]
    [InlineData("PVC")]
    [InlineData("PP")]
    [InlineData("GFK")]
    [InlineData("Polyethylen")]
    [InlineData("Kunststoff")]
    [InlineData("Plastik")]
    [InlineData("Faserzement")]
    public void IsKunststoff_TrueFuerKunststoffe(string material)
        => Assert.True(MaterialPlausibilityFilter.IsKunststoff(material));

    [Theory]
    [InlineData("Beton")]
    [InlineData("Steinzeug")]
    [InlineData("Guss")]
    [InlineData("Stahl")]
    public void IsKunststoff_FalseFuerHarteMaterialien(string material)
        => Assert.False(MaterialPlausibilityFilter.IsKunststoff(material));

    [Fact]
    public void IsKunststoff_LeererInputFalsch()
    {
        Assert.False(MaterialPlausibilityFilter.IsKunststoff(""));
        Assert.False(MaterialPlausibilityFilter.IsKunststoff(null!));
    }

    // ── MapMaterialToAedCode ──────────────────────────────────────────────

    [Theory]
    [InlineData("PE",       "AEDXO")]
    [InlineData("PP",       "AEDXP")]
    [InlineData("PVC",      "AEDXQ")]
    [InlineData("Beton",    "AEDXG")]
    [InlineData("Steinzeug","AEDXU")]
    [InlineData("GFK",      "AEDXH")]
    [InlineData("Stahl",    "AEDXI")]
    [InlineData("Guss",     "AEDXJ")]
    [InlineData("Faserzement", "AEDXK")]
    public void MapMaterialToAedCode_LiefertPassendenUntercode(string material, string expected)
        => Assert.Equal(expected, MaterialPlausibilityFilter.MapMaterialToAedCode(material));

    [Fact]
    public void MapMaterialToAedCode_UnbekanntLiefertAed()
        => Assert.Equal("AED", MaterialPlausibilityFilter.MapMaterialToAedCode("Unobtanium"));

    [Fact]
    public void MapMaterialToAedCode_LeerLiefertAed()
    {
        Assert.Equal("AED", MaterialPlausibilityFilter.MapMaterialToAedCode(""));
        Assert.Equal("AED", MaterialPlausibilityFilter.MapMaterialToAedCode(null!));
    }

    // ── Apply ─────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_OhneMaterial_NichtsEntfernt()
    {
        var list = new List<RawVideoDetection> { Det("BBF", 1.0) };
        var removed = MaterialPlausibilityFilter.Apply(list, material: null);
        Assert.Equal(0, removed);
        Assert.Single(list);
    }

    [Fact]
    public void Apply_BetonMaterial_NichtsEntfernt()
    {
        var list = new List<RawVideoDetection> { Det("BBF", 1.0), Det("BBD", 2.0) };
        var removed = MaterialPlausibilityFilter.Apply(list, material: "Beton");
        Assert.Equal(0, removed);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Apply_KunststoffOhneBegleitschaden_BBFundBBDEntfernt()
    {
        var list = new List<RawVideoDetection>
        {
            Det("BBF", 1.0),
            Det("BBD", 5.0),
            Det("BBA", 10.0), // Inkrustation: nicht gefiltert
        };
        var removed = MaterialPlausibilityFilter.Apply(list, material: "PE");
        Assert.Equal(2, removed);
        Assert.Single(list);
        Assert.Equal("BBA", list[0].VsaCodeHint);
    }

    [Fact]
    public void Apply_KunststoffMitBegleitschaden_BBFBleibt()
    {
        var list = new List<RawVideoDetection>
        {
            Det("BAB", 5.0),   // Riss in 5m
            Det("BBF", 5.5),   // Infiltration nah dran (<2m) → behalten
            Det("BBF", 20.0),  // Infiltration weit weg → entfernen
        };
        var removed = MaterialPlausibilityFilter.Apply(list, material: "PVC");
        Assert.Equal(1, removed);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, d => d.VsaCodeHint == "BAB");
        Assert.Contains(list, d => d.VsaCodeHint == "BBF" && d.MeterStart == 5.5);
    }

    [Fact]
    public void Apply_LeereListe_KeinFehler()
    {
        var list = new List<RawVideoDetection>();
        var removed = MaterialPlausibilityFilter.Apply(list, material: "PE");
        Assert.Equal(0, removed);
    }
}
