using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Ein Copilot verzerrt die Daten, die er selbst erzeugt: Wer einen Vorschlag
/// sieht, codiert anders. Belegt am 2026-08-07 — dieselbe Stelle wurde mit
/// sichtbarem Modellrahmen als Bogen codiert und ohne ihn als Rohrverbindung
/// mit Knick. Deshalb darf ein Modell niemals an Material gemessen werden, das
/// unter dem Einfluss seiner eigenen Vorschlaege entstanden ist.
/// </summary>
public sealed class SuggestionProvenancePolicyTests
{
    [Fact]
    public void Ohne_Herkunftsangabe_gilt_ein_Sample_nicht_als_unabhaengig()
    {
        // Altbestand traegt das Feld nicht. Unbekannt ist nicht sauber —
        // sonst wuerde die gesamte Vergangenheit stillschweigend als Messgrundlage gelten.
        var sample = new TrainingSample { SampleId = "alt" };

        Assert.False(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(sample));
        Assert.Equal(
            TrainingSampleSuggestionOrigin.Unknown,
            SuggestionProvenancePolicy.ResolveOrigin(sample));
    }

    [Fact]
    public void Ein_ausdruecklich_unabhaengig_codiertes_Sample_darf_messen()
    {
        var sample = new TrainingSample
        {
            SampleId = "eigen",
            SuggestionProvenance = new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.Independent
            }
        };

        Assert.True(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(sample));
    }

    [Fact]
    public void Ein_Sample_mit_sichtbarem_Vorschlag_darf_niemals_messen()
    {
        var sample = new TrainingSample
        {
            SampleId = "beeinflusst",
            SuggestionProvenance = new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.SuggestionShown,
                ModelId = "bcc_nc15_seed44_20260808",
                SuggestedCode = "BCCYB",
                SuggestedConfidence = 0.57
            }
        };

        Assert.False(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(sample));
    }

    [Fact]
    public void Eine_Korrektur_traegt_neue_Information_eine_blosse_Zustimmung_nicht()
    {
        var zugestimmt = MitVorschlag("BCCYB", korrigiert: false);
        var korrigiert = MitVorschlag("BCCYB", korrigiert: true);

        Assert.False(SuggestionProvenancePolicy.CarriesNewInformation(zugestimmt));
        Assert.True(SuggestionProvenancePolicy.CarriesNewInformation(korrigiert));
    }

    [Fact]
    public void Ein_unabhaengiges_Sample_traegt_immer_neue_Information()
    {
        var sample = new TrainingSample
        {
            SampleId = "eigen",
            SuggestionProvenance = new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.Independent
            }
        };

        Assert.True(SuggestionProvenancePolicy.CarriesNewInformation(sample));
    }

    [Fact]
    public void Ein_abweichender_Code_gilt_als_Korrektur_auch_ohne_gesetztes_Flag()
    {
        // Der Codiermodus setzt Corrected nicht in jedem Weg. Weicht der
        // gespeicherte Code vom Vorschlag ab, ist das eine Korrektur.
        var sample = MitVorschlag("BCCYB", korrigiert: false);
        sample.Code = "BAJC";

        Assert.True(SuggestionProvenancePolicy.CarriesNewInformation(sample));
    }

    [Fact]
    public void Der_Grund_nennt_das_Modell_damit_er_im_Bericht_brauchbar_ist()
    {
        var sample = MitVorschlag("BCCYB", korrigiert: false);

        var grund = SuggestionProvenancePolicy.DescribeMeasurementBias(sample);

        Assert.Contains("bcc_nc15_seed44_20260808", grund);
    }

    [Fact]
    public void Ohne_Sample_wird_nichts_angenommen()
    {
        Assert.False(SuggestionProvenancePolicy.IsUnbiasedForMeasurement(null));
        Assert.False(SuggestionProvenancePolicy.CarriesNewInformation(null));
        Assert.Equal(
            TrainingSampleSuggestionOrigin.Unknown,
            SuggestionProvenancePolicy.ResolveOrigin(null));
    }

    private static TrainingSample MitVorschlag(string vorschlag, bool korrigiert) =>
        new()
        {
            SampleId = "mit-vorschlag",
            Code = vorschlag,
            Corrected = korrigiert,
            SuggestionProvenance = new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.SuggestionShown,
                ModelId = "bcc_nc15_seed44_20260808",
                ModelSha256 = "36b31d55d7da1931f3d04c2582da92186be19d61d65af96b1b8548474eb0ca4a",
                SuggestedCode = vorschlag,
                SuggestedConfidence = 0.57
            }
        };
}
