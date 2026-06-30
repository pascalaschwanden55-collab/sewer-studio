using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer DefaultDescriptionBuilder (IST-Verhalten).
/// </summary>
public sealed class DefaultDescriptionBuilderTests
{
    private static CodeDefinition MakeDef(string title, bool requiresRange = false, List<CodeParameter>? parameters = null)
        => new()
        {
            Code = "BAB",
            Title = title,
            RequiresRange = requiresRange,
            Parameters = parameters ?? new List<CodeParameter>()
        };

    // ── Keine Parameter ───────────────────────────────────────────────────────

    [Fact]
    public void Build_ohne_parameter_gibt_nur_titel_zurueck()
    {
        var def = MakeDef("Riss");
        var result = DefaultDescriptionBuilder.Build(def, null, null, null);
        Assert.Equal("Riss", result);
    }

    [Fact]
    public void Build_leere_parameter_dict_gibt_nur_titel_zurueck()
    {
        var def = MakeDef("Verformung");
        var result = DefaultDescriptionBuilder.Build(def, new Dictionary<string, string>(), null, null);
        Assert.Equal("Verformung", result);
    }

    // ── Parameter werden angefuegt ────────────────────────────────────────────

    [Fact]
    public void Build_parameter_ohne_einheit_werden_angehaengt()
    {
        var def = new CodeDefinition
        {
            Code = "BAA",
            Title = "Verformung",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Richtung", Type = "string", Unit = null }
            }
        };

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Richtung", "vertikal" }
        };

        var result = DefaultDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Verformung (Richtung=vertikal)", result);
    }

    [Fact]
    public void Build_parameter_mit_einheit_werden_angehaengt()
    {
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Breite", Type = "number", Unit = "mm" }
            }
        };

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Breite", "5" }
        };

        var result = DefaultDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss (Breite=5 mm)", result);
    }

    [Fact]
    public void Build_leere_parameterwerte_werden_ignoriert()
    {
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Breite", Type = "number", Unit = "mm" },
                new() { Name = "Anmerkung", Type = "string" }
            }
        };

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Breite", "5" },
            { "Anmerkung", string.Empty }  // leer -> ignoriert
        };

        var result = DefaultDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss (Breite=5 mm)", result);
    }

    // ── Strecke ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_streckenschaden_wird_angehaengt_wenn_requiresRange()
    {
        var def = MakeDef("Korrosion", requiresRange: true);
        var result = DefaultDescriptionBuilder.Build(def, null, 2.5, 8.0);
        Assert.Equal("Korrosion (Strecke 2.50-8.00 m)", result);
    }

    [Fact]
    public void Build_streckenschaden_wird_ignoriert_wenn_requiresRange_false()
    {
        var def = MakeDef("Riss", requiresRange: false);
        var result = DefaultDescriptionBuilder.Build(def, null, 2.5, 8.0);
        Assert.Equal("Riss", result);
    }

    [Fact]
    public void Build_streckenschaden_wird_ignoriert_wenn_meter_fehlt()
    {
        var def = MakeDef("Ablagerung", requiresRange: true);
        var result = DefaultDescriptionBuilder.Build(def, null, null, 8.0);  // meterStart fehlt
        Assert.Equal("Ablagerung", result);
    }

    // ── Kombination ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_parameter_und_strecke_werden_kombiniert()
    {
        var def = new CodeDefinition
        {
            Code = "BAF",
            Title = "Oberflaechenschaden",
            RequiresRange = true,
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Grad", Type = "string" }
            }
        };

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Grad", "stark" }
        };

        var result = DefaultDescriptionBuilder.Build(def, parameters, 1.0, 3.0);
        Assert.Equal("Oberflaechenschaden (Grad=stark, Strecke 1.00-3.00 m)", result);
    }
}
