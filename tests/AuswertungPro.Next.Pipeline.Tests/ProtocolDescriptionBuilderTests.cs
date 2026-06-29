using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer ProtocolDescriptionBuilder (IST-Verhalten aus ObservationCatalogViewModel).
/// </summary>
public sealed class ProtocolDescriptionBuilderTests
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
        Assert.Equal("Riss", ProtocolDescriptionBuilder.Build(def, null, null, null));
    }

    [Fact]
    public void Build_leere_parameter_dict_gibt_nur_titel_zurueck()
    {
        var def = MakeDef("Verformung");
        Assert.Equal("Verformung", ProtocolDescriptionBuilder.Build(def, new Dictionary<string, string>(), null, null));
    }

    // ── Parameter per DataKey-Lookup ─────────────────────────────────────────

    [Fact]
    public void Build_parameter_werden_per_datakey_gefunden()
    {
        // Hinweis: DataKey "Q1" trifft auch die Quantifizierungs-Aliase -> doppelter Eintrag ist IST-Verhalten
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Breite", DataKey = "Hoehe", Type = "number", Unit = "mm" }
            }
        };
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Hoehe", "5" } };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        // Format: "{Wert}{Einheit}" (kein Leerzeichen vor Einheit wenn Einheit direkt angehaengt)
        Assert.Equal("Riss: 5mm", result);
    }

    [Fact]
    public void Build_parameter_ohne_einheit_werden_ohne_einheit_angehaengt()
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
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Richtung", "vertikal" } };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Verformung: vertikal", result);
    }

    [Fact]
    public void Build_parameter_fallback_auf_name_wenn_datakey_fehlt()
    {
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Breite", DataKey = null, Type = "number", Unit = "mm" }
            }
        };
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Breite", "3" } };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss: 3mm", result);
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
            { "Anmerkung", string.Empty }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss: 5mm", result);
    }

    // ── Uhrzeiten ─────────────────────────────────────────────────────────────

    [Fact]
    public void Build_uhrzeiten_von_bis_werden_angehaengt()
    {
        var def = MakeDef("Riss");
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vsa.uhr.von", "8" },
            { "vsa.uhr.bis", "3" }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss: von 8 Uhr bis 3 Uhr", result);
    }

    [Fact]
    public void Build_uhrzeit_von_ohne_bis_wird_als_bei_angehaengt()
    {
        var def = MakeDef("Riss");
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vsa.uhr.von", "12" }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss: bei 12 Uhr", result);
    }

    [Fact]
    public void Build_uhrzeiten_per_clockpos_aliase()
    {
        var def = MakeDef("Riss");
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ClockPos1", "6" },
            { "ClockPos2", "9" }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss: von 6 Uhr bis 9 Uhr", result);
    }

    // ── Quantifizierung ───────────────────────────────────────────────────────

    [Fact]
    public void Build_quantifizierung_q1_wird_mit_prozent_angehaengt()
    {
        var def = MakeDef("Ablagerung");
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vsa.q1", "25" }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Ablagerung: 25%", result);
    }

    [Fact]
    public void Build_q1_und_q2_werden_beide_angehaengt()
    {
        var def = MakeDef("Riss");
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vsa.q1", "10" },
            { "vsa.q2", "20" }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Riss: 10%, 20%", result);
    }

    // ── Strecke ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_streckenschaden_wird_angehaengt_wenn_requiresRange()
    {
        var def = MakeDef("Korrosion", requiresRange: true);
        var result = ProtocolDescriptionBuilder.Build(def, null, 2.5, 8.0);
        Assert.Equal("Korrosion: Strecke 2.50-8.00 m", result);
    }

    [Fact]
    public void Build_streckenschaden_wird_ignoriert_wenn_requiresRange_false()
    {
        var def = MakeDef("Riss", requiresRange: false);
        var result = ProtocolDescriptionBuilder.Build(def, null, 2.5, 8.0);
        Assert.Equal("Riss", result);
    }

    // ── Kombination ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_parameter_und_uhrzeiten_kombiniert()
    {
        var def = new CodeDefinition
        {
            Code = "BAJ",
            Title = "Versatz",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Versatzbreite", DataKey = "Hoehe", Type = "number", Unit = "mm" }
            }
        };
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hoehe", "10" },
            { "vsa.uhr.von", "3" },
            { "vsa.uhr.bis", "9" }
        };

        var result = ProtocolDescriptionBuilder.Build(def, parameters, null, null);
        Assert.Equal("Versatz: 10mm, von 3 Uhr bis 9 Uhr", result);
    }

    // ── GetFirstParameter ────────────────────────────────────────────────────

    [Fact]
    public void GetFirstParameter_gibt_ersten_nicht_leeren_wert_zurueck()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vsa.uhr.von", "8" },
            { "ClockPos1", "8" }
        };

        var result = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "vsa.uhr.von", "ClockPos1");
        Assert.Equal("8", result);
    }

    [Fact]
    public void GetFirstParameter_fallback_auf_zweiten_schluessel()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ClockPos1", "6" }
        };

        var result = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "vsa.uhr.von", "ClockPos1");
        Assert.Equal("6", result);
    }

    [Fact]
    public void GetFirstParameter_gibt_null_zurueck_wenn_kein_schluessel_existiert()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assert.Null(ProtocolDescriptionBuilder.GetFirstParameter(parameters, "vsa.uhr.von", "ClockPos1"));
    }

    [Fact]
    public void GetFirstParameter_gibt_null_zurueck_fuer_leere_dict()
    {
        Assert.Null(ProtocolDescriptionBuilder.GetFirstParameter(null, "vsa.uhr.von"));
    }
}
