using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Arbeitspunkt eines Bogen-Kandidaten ist gemessen und gehoert zum Gewicht.
/// Diese Regel bindet ihn an genau ein Artefakt: Passt die Kandidaten-ID oder der
/// Gewicht-Hash nicht, wird der Kandidat nicht als Vorschlagsquelle angeboten.
/// Fail-closed, wie der Sidecar-Waechter selbst.
/// </summary>
public sealed class BendSuggestionCalibrationPolicyTests
{
    private const string Id = "bcc_nc15_seed46_20260808";
    private const string Sha = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";

    [Fact]
    public void Eine_passende_Kalibrierung_ergibt_die_Grenzen_des_Gewichts()
    {
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(Kalibrierung(), Id, Sha);

        Assert.True(ergebnis.IsUsable);
        Assert.Equal(0.50, ergebnis.Options!.MinConfidence, 3);
        Assert.Equal(0.70, ergebnis.Options!.StrongConfidence, 3);
        Assert.Empty(ergebnis.Reason);
    }

    [Fact]
    public void Ohne_Kalibrierung_wird_der_Kandidat_nicht_angeboten()
    {
        // Ein Gewicht ohne gemessenen Arbeitspunkt ist unbrauchbar: Seed 45 fand bei
        // conf 0,50 nur zwei von zehn Boegen, Seed 46 sieben. Ohne Messung waere die
        // Wahl geraten.
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(null, Id, Sha);

        Assert.False(ergebnis.IsUsable);
        Assert.Null(ergebnis.Options);
        Assert.Contains("Arbeitspunkt", ergebnis.Reason);
    }

    [Fact]
    public void Eine_Kalibrierung_eines_anderen_Kandidaten_wird_abgewiesen()
    {
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(
            Kalibrierung(), "bcc_nc15_seed44_20260808", Sha);

        Assert.False(ergebnis.IsUsable);
        Assert.Contains("Kandidat", ergebnis.Reason);
    }

    [Fact]
    public void Ein_abweichender_Gewicht_Hash_wird_abgewiesen()
    {
        // Wird das Gewicht ausgetauscht, gilt der gemessene Arbeitspunkt nicht mehr.
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(Kalibrierung(), Id, new string('b', 64));

        Assert.False(ergebnis.IsUsable);
        Assert.Contains("Gewicht", ergebnis.Reason);
    }

    [Fact]
    public void Der_Hash_darf_in_Gross_und_Kleinschreibung_stehen()
    {
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(Kalibrierung(), Id, Sha.ToUpperInvariant());

        Assert.True(ergebnis.IsUsable);
    }

    [Theory]
    [InlineData(0.0, 0.70)]
    [InlineData(-0.1, 0.70)]
    [InlineData(1.1, 1.2)]
    [InlineData(0.5, 0.4)]   // stark darf nie unter dem Arbeitspunkt liegen
    [InlineData(0.5, 1.1)]
    public void Unbrauchbare_Grenzen_werden_abgewiesen(double min, double strong)
    {
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(
            Kalibrierung() with { MinConfidence = min, StrongConfidence = strong }, Id, Sha);

        Assert.False(ergebnis.IsUsable);
        Assert.Contains("Grenze", ergebnis.Reason);
    }

    [Fact]
    public void Eine_Kalibrierung_ohne_Herkunftsangabe_wird_abgewiesen()
    {
        // Ein Arbeitspunkt ohne Beleg ist geraten. Genau das soll die Regel verhindern.
        var ergebnis = BendSuggestionCalibrationPolicy.Resolve(
            Kalibrierung() with { Source = "  " }, Id, Sha);

        Assert.False(ergebnis.IsUsable);
        Assert.Contains("Beleg", ergebnis.Reason);
    }

    [Fact]
    public void Ohne_Kandidatenangabe_wird_nichts_angenommen()
    {
        Assert.False(BendSuggestionCalibrationPolicy.Resolve(Kalibrierung(), "  ", Sha).IsUsable);
        Assert.False(BendSuggestionCalibrationPolicy.Resolve(Kalibrierung(), Id, "  ").IsUsable);
    }

    private static BendSuggestionCalibration Kalibrierung() => new()
    {
        CandidateId = Id,
        WeightSha256 = Sha,
        MinConfidence = 0.50,
        StrongConfidence = 0.70,
        Source = "Videomessung 2026-08-08, 8 Haltungen, 64 Blindurteile"
    };
}
