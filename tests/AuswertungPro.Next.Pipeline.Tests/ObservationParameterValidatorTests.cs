using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer ObservationParameterValidator (IST-Verhalten aus ObservationParameterViewModel.Validate).
/// </summary>
public sealed class ObservationParameterValidatorTests
{
    // ── Required ──────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_required_leerer_wert_ist_ungueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Richtung", "string", required: true, allowedValues: null, value: "", out var error);

        Assert.False(ok);
        Assert.Contains("erforderlich", error);
    }

    [Fact]
    public void Validate_required_nicht_leer_ist_gueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Richtung", "string", required: true, allowedValues: null, value: "A", out var error);

        Assert.True(ok);
        Assert.Empty(error);
    }

    [Fact]
    public void Validate_nicht_required_leerer_wert_ist_gueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Richtung", "string", required: false, allowedValues: null, value: "", out var error);

        Assert.True(ok);
        Assert.Empty(error);
    }

    // ── Enum ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_enum_erlaubter_wert_ist_gueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Art", "enum", required: false,
            allowedValues: new[] { "A", "B", "C" },
            value: "A", out var error);

        Assert.True(ok);
        Assert.Empty(error);
    }

    [Fact]
    public void Validate_enum_case_insensitive()
    {
        var ok = ObservationParameterValidator.Validate(
            "Art", "enum", required: false,
            allowedValues: new[] { "A", "B", "C" },
            value: "a", out var error);

        Assert.True(ok);
    }

    [Fact]
    public void Validate_enum_unbekannter_wert_ist_ungueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Art", "enum", required: false,
            allowedValues: new[] { "A", "B", "C" },
            value: "X", out var error);

        Assert.False(ok);
        Assert.Contains("ungueltigen Wert", error);
    }

    // ── Number ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_number_gueltiger_wert_ist_gueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Breite", "number", required: false, allowedValues: null, value: "3.5", out var error);

        Assert.True(ok);
    }

    [Fact]
    public void Validate_number_komma_statt_punkt_ist_gueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Breite", "number", required: false, allowedValues: null, value: "3,5", out var error);

        Assert.True(ok);
    }

    [Fact]
    public void Validate_number_text_wert_ist_ungueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Breite", "number", required: false, allowedValues: null, value: "abc", out var error);

        Assert.False(ok);
        Assert.Contains("numerisch", error);
    }

    // ── Clock ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_clock_gueltiger_wert_0_bis_12()
    {
        for (int i = 0; i <= 12; i++)
        {
            var ok = ObservationParameterValidator.Validate(
                "Uhrlage", "clock", required: false, allowedValues: null,
                value: i.ToString(), out var error);
            Assert.True(ok, $"Wert {i} sollte gueltig sein");
        }
    }

    [Fact]
    public void Validate_clock_wert_13_ist_ungueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Uhrlage", "clock", required: false, allowedValues: null, value: "13", out var error);

        Assert.False(ok);
        Assert.Contains("00 und 12", error);
    }

    [Fact]
    public void Validate_clock_text_wert_ist_ungueltig()
    {
        var ok = ObservationParameterValidator.Validate(
            "Uhrlage", "clock", required: false, allowedValues: null, value: "Scheitel", out var error);

        Assert.False(ok);
        Assert.Contains("00 und 12", error);
    }

    // ── Whitespace-Trimming ───────────────────────────────────────────────────

    [Fact]
    public void Validate_wert_mit_whitespace_wird_getrimmt()
    {
        var ok = ObservationParameterValidator.Validate(
            "Breite", "number", required: false, allowedValues: null, value: "  5  ", out _);
        Assert.True(ok);
    }

    [Fact]
    public void Validate_whitespace_only_wert_wird_als_leer_behandelt()
    {
        // Nicht-required -> leer -> gueltig
        var ok = ObservationParameterValidator.Validate(
            "Breite", "number", required: false, allowedValues: null, value: "   ", out _);
        Assert.True(ok);
    }
}
