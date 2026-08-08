using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.ModelPromotion;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Entscheidet, ob ein neues Modell das bestehende ersetzen darf.
///
/// Die Regeln sind aus Fehlern dieser Woche entstanden, jeder davon teuer:
/// Ein Einzellauf sah wie ein Fortschritt aus und war Seed-Glueck (die Spanne
/// ueber drei identische Laeufe lag bei 20 bis 28 von 37). Ein Vergleich gegen
/// unterschiedliche Bestaende misst den Bestand, nicht das Modell. Und ein
/// besserer Recall bei schlechteren Fehlalarmen ist kein Fortschritt.
/// </summary>
public sealed class ModelPromotionPolicyTests
{
    private const string Set = "detect_benchmark_v1";
    private const string Sha = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    [Fact]
    public void Eine_Verbesserung_groesser_als_die_Streuung_erlaubt_den_Tausch()
    {
        var entscheidung = Entscheide(
            bestehend: [0.50, 0.52, 0.54],
            kandidat: [0.70, 0.71, 0.72]);

        Assert.True(entscheidung.Promote);
        Assert.Contains("groesser als die Streuung", entscheidung.Reason);
    }

    [Fact]
    public void Eine_Verbesserung_innerhalb_der_Streuung_reicht_nicht()
    {
        // Genau der Fall, der ohne diese Regel als Fortschritt gegolten haette.
        var entscheidung = Entscheide(
            bestehend: [0.50, 0.60, 0.70],
            kandidat: [0.62, 0.65, 0.68]);

        Assert.False(entscheidung.Promote);
        Assert.Contains("Streuung", entscheidung.Reason);
    }

    [Fact]
    public void Ein_Einzellauf_ist_kein_Beleg()
    {
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Messungen([0.50, 0.52, 0.54]),
            Candidate = Messungen([0.90])
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Laeufe", entscheidung.Reason);
    }

    [Fact]
    public void Verschiedene_Messbestaende_duerfen_nie_verglichen_werden()
    {
        // Sonst misst der Vergleich den Bestand statt das Modell.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Messungen([0.50, 0.52, 0.54]),
            Candidate = Messungen([0.80, 0.81, 0.82], set: "detect_benchmark_v2")
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Messbestand", entscheidung.Reason);
    }

    [Fact]
    public void Ein_abweichender_Bestands_Hash_wird_genauso_abgewiesen()
    {
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Messungen([0.50, 0.52, 0.54]),
            Candidate = Messungen([0.80, 0.81, 0.82], sha: new string('b', 64))
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Messbestand", entscheidung.Reason);
    }

    [Fact]
    public void Mehr_Treffer_bei_mehr_Fehlalarmen_ist_kein_Fortschritt()
    {
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Messungen([0.50, 0.51, 0.52], fehlalarm: [0.10, 0.11, 0.12]),
            Candidate = Messungen([0.80, 0.81, 0.82], fehlalarm: [0.40, 0.41, 0.42])
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Fehlalarm", entscheidung.Reason);
    }

    [Fact]
    public void Eine_Fehlalarmaenderung_innerhalb_der_Streuung_blockiert_nicht()
    {
        // Die Fehlalarmquote schwankte ueber drei identische Laeufe zwischen 8 und
        // 23 Prozent. Eine kleine Verschlechterung darf einen echten Gewinn nicht
        // aufhalten — sonst blockiert das Rauschen jeden Fortschritt.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Messungen([0.50, 0.51, 0.52], fehlalarm: [0.08, 0.15, 0.23]),
            Candidate = Messungen([0.80, 0.81, 0.82], fehlalarm: [0.12, 0.16, 0.20])
        });

        Assert.True(entscheidung.Promote);
    }

    [Fact]
    public void Das_Ergebnis_nennt_die_Spanne_statt_des_besten_Laufs()
    {
        // Der beste von drei liegt systematisch ueber dem Erwartungswert.
        var entscheidung = Entscheide(
            bestehend: [0.50, 0.52, 0.54],
            kandidat: [0.70, 0.71, 0.78]);

        Assert.Equal(0.70, entscheidung.CandidateMinimum, 3);
        Assert.Equal(0.78, entscheidung.CandidateMaximum, 3);
        Assert.Equal(0.73, entscheidung.CandidateMean, 2);
    }

    [Fact]
    public void Ohne_Messungen_wird_nichts_getauscht()
    {
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = [],
            Candidate = Messungen([0.90, 0.91, 0.92])
        });

        Assert.False(entscheidung.Promote);
    }

    [Fact]
    public void Ein_schlechterer_Kandidat_wird_abgewiesen()
    {
        var entscheidung = Entscheide(
            bestehend: [0.70, 0.71, 0.72],
            kandidat: [0.50, 0.51, 0.52]);

        Assert.False(entscheidung.Promote);
    }

    private static ModelPromotionDecision Entscheide(
        IReadOnlyList<double> bestehend,
        IReadOnlyList<double> kandidat)
        => ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Messungen(bestehend),
            Candidate = Messungen(kandidat)
        });

    private static IReadOnlyList<ModelMeasurement> Messungen(
        IReadOnlyList<double> recall,
        IReadOnlyList<double>? fehlalarm = null,
        string set = Set,
        string sha = Sha)
        => recall
            .Select((wert, index) => new ModelMeasurement(
                set, sha, 42 + index, wert, fehlalarm?[index] ?? 0.10))
            .ToList();
}
