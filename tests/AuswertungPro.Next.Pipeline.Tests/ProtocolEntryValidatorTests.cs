using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer ProtocolEntryValidator (IST-Verhalten).
/// </summary>
public sealed class ProtocolEntryValidatorTests
{
    // ── Hilfsmethoden ────────────────────────────────────────────────────────

    private static VsaFieldInputs ValidInputs(bool isStreckenschaden = false)
        => new()
        {
            Distanz = "12.5",
            Video = "01:30",
            UhrVon = "6",
            UhrBis = "9",
            Q1 = "50",
            Q2 = string.Empty,
            Strecke = "A1",
            Ez = "EZ2",
            Schachtbereich = "A",
            IsStreckenschaden = isStreckenschaden
        };

    private static VsaFieldInputs EmptyInputs(bool isStreckenschaden = false)
        => new()
        {
            Distanz = string.Empty,
            Video = string.Empty,
            UhrVon = null,
            UhrBis = null,
            Q1 = string.Empty,
            Q2 = string.Empty,
            Strecke = null,
            Ez = null,
            Schachtbereich = null,
            IsStreckenschaden = isStreckenschaden
        };

    // ── Alles gueltig ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_alle_gueltigen_werte_geben_keine_fehler()
    {
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", ValidInputs());
        Assert.Empty(result.Errors);
        Assert.True(result.DistanzOk);
        Assert.True(result.VideoOk);
        Assert.True(result.UhrVonOk);
        Assert.True(result.UhrBisOk);
        Assert.True(result.Q1Ok);
        Assert.True(result.Q2Ok);
        Assert.True(result.StreckeOk);
        Assert.True(result.EzOk);
        Assert.True(result.SchachtbereichOk);
    }

    // ── Leerer Code ──────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_leerer_code_ohne_distanz_ist_ok_kein_pflichtfeld()
    {
        // Mit leerem Code und allen leeren Feldern: kein Fehler (Distanz ist nur Pflicht wenn hasCode)
        var result = ProtocolEntryValidator.ValidateVsaFields(string.Empty, EmptyInputs(), requireDistanz: true);
        // Distanz soll ok sein weil hasCode=false => kein Pflicht-Trigger
        Assert.True(result.DistanzOk);
    }

    // ── Distanz ───────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_distanz_leer_mit_code_und_requireDistanz_ist_fehler()
    {
        var inputs = EmptyInputs() with { Distanz = string.Empty };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs, requireDistanz: true);
        Assert.False(result.DistanzOk);
        Assert.Contains(result.Errors, e => e.Contains("Distanz"));
    }

    [Fact]
    public void ValidateVsaFields_distanz_ungueltig_ergibt_fehler()
    {
        var inputs = EmptyInputs() with { Distanz = "xyz" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.DistanzOk);
        Assert.Contains(result.Errors, e => e.Contains("Distanz"));
    }

    [Fact]
    public void ValidateVsaFields_distanz_leer_mit_requireDistanz_false_ist_ok()
    {
        var inputs = EmptyInputs() with { Distanz = string.Empty };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs, requireDistanz: false);
        Assert.True(result.DistanzOk);
    }

    // ── Video ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_ungueltiges_video_format_ergibt_fehler()
    {
        var inputs = ValidInputs() with { Video = "abc" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.VideoOk);
        Assert.Contains(result.Errors, e => e.Contains("Video"));
    }

    // ── Uhrzeiger ─────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_uhr_bis_ohne_uhr_von_ergibt_fehler()
    {
        var inputs = EmptyInputs() with { Distanz = "5", UhrBis = "3" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.UhrVonOk);
        Assert.Contains(result.Errors, e => e.Contains("Uhr von") && e.Contains("Uhr bis"));
    }

    [Fact]
    public void ValidateVsaFields_ungueltiger_uhrzeitwert_ergibt_fehler()
    {
        var inputs = ValidInputs() with { UhrVon = "13" };  // > 12
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.UhrVonOk);
    }

    // ── Strecke ───────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_ungueltiges_streckenformat_ergibt_fehler()
    {
        var inputs = ValidInputs() with { Strecke = "D9" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.StreckeOk);
        Assert.Contains(result.Errors, e => e.Contains("Strecke"));
    }

    [Fact]
    public void ValidateVsaFields_streckenschaden_ohne_strecke_ergibt_fehler()
    {
        var inputs = EmptyInputs(isStreckenschaden: true) with { Distanz = "5.0" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.StreckeOk);
        Assert.Contains(result.Errors, e => e.Contains("Streckenschaden"));
    }

    // ── EZ ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_ez_ausserhalb_bereich_ergibt_fehler()
    {
        var inputs = ValidInputs() with { Ez = "EZ9" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.EzOk);
        Assert.Contains(result.Errors, e => e.Contains("EZ"));
    }

    // ── Schachtbereich ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_ungueltiger_schachtbereich_ergibt_fehler()
    {
        var inputs = ValidInputs() with { Schachtbereich = "C" };  // C nicht erlaubt
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs);
        Assert.False(result.SchachtbereichOk);
        Assert.Contains(result.Errors, e => e.Contains("Schachtbereich"));
    }

    // ── Katalog-abhaengige Pruefungen ─────────────────────────────────────────

    [Fact]
    public void ValidateVsaFields_clock_parameter_ohne_uhr_von_ergibt_fehler()
    {
        // Katalog-Definition mit Clock-Parameter
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Lage", Type = "clock", Required = true }
            }
        };

        var inputs = EmptyInputs() with { Distanz = "5.0" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs, def);
        Assert.False(result.UhrVonOk);
        Assert.Contains(result.Errors, e => e.Contains("Uhr von") && e.Contains("erforderlich"));
    }

    [Fact]
    public void ValidateVsaFields_quant1_ohne_katalog_definition_ergibt_fehler()
    {
        // Kein Quant1-Parameter im Katalog, aber Eingabe hat Q1
        var def = new CodeDefinition
        {
            Code = "BCD",
            Title = "Rohranfang",
            Parameters = new List<CodeParameter>()  // kein Quant1
        };

        var inputs = EmptyInputs() with { Distanz = "5.0", Q1 = "50" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BCD", inputs, def);
        Assert.False(result.Q1Ok);
        Assert.Contains(result.Errors, e => e.Contains("Quantifizierung 1") && e.Contains("nicht vorgesehen"));
    }

    [Fact]
    public void ValidateVsaFields_quant2_ohne_katalog_definition_ergibt_fehler()
    {
        var def = new CodeDefinition
        {
            Code = "BCD",
            Title = "Rohranfang",
            Parameters = new List<CodeParameter>()
        };

        var inputs = EmptyInputs() with { Distanz = "5.0", Q2 = "30" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BCD", inputs, def);
        Assert.False(result.Q2Ok);
        Assert.Contains(result.Errors, e => e.Contains("Quantifizierung 2") && e.Contains("nicht vorgesehen"));
    }

    [Fact]
    public void ValidateVsaFields_quant1_mit_katalog_parameter_quant1_ist_ok()
    {
        var def = new CodeDefinition
        {
            Code = "BAB",
            Title = "Riss",
            Parameters = new List<CodeParameter>
            {
                new() { Name = "Quant1", Type = "number", Required = false }
            }
        };

        var inputs = EmptyInputs() with { Distanz = "5.0", Q1 = "25" };
        var result = ProtocolEntryValidator.ValidateVsaFields("BAB", inputs, def);
        Assert.True(result.Q1Ok);
        Assert.Empty(result.Errors);
    }
}
