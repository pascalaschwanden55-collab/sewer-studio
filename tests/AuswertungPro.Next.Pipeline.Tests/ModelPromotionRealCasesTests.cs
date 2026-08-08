using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.ModelPromotion;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die Regel an den echten Messungen dieser Woche. Jeder Fall ist einmal
/// tatsaechlich aufgetreten — zweimal davon haette ohne diese Regel eine falsche
/// Aussage im Bericht gestanden.
/// </summary>
public sealed class ModelPromotionRealCasesTests
{
    private const string Set = "detect_benchmark_v1";
    private const string Sha = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    [Fact]
    public void Der_nc15_Kandidat_ersetzt_das_Ein_Klassen_Modell_NICHT()
    {
        // Gemessen 2026-08-08 auf detect_benchmark_v1, conf 0,25, je drei Seeds:
        // Ein-Klassen-Modell 21–26 von 37, nc:15 20–28 von 37. Aus dem ersten
        // Seed allein (28) war "besser" geworden — die Spannen ueberlappen jedoch
        // vollstaendig.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Von37([21, 24, 26]),
            Candidate = Von37([20, 25, 28])
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Streuung", entscheidung.Reason);
    }

    [Fact]
    public void Der_einzelne_Glueckslauf_alleine_wuerde_abgewiesen()
    {
        // Genau die Lage vom Vormittag: ein Lauf mit 28 gegen drei mit 21–26.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Von37([21, 24, 26]),
            Candidate = Von37([28])
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Laeufe", entscheidung.Reason);
    }

    [Fact]
    public void Ein_Modellwechsel_zusammen_mit_einem_Bestandswechsel_wird_abgewiesen()
    {
        // Der Fall, den wir Kimi gegenueber praezisiert haben: Der Messbestand
        // waechst laufend weiter; gemessen wird nur gegen eine eingefrorene
        // Version.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Von37([21, 24, 26]),
            Candidate = Von37([33, 34, 35], set: "detect_benchmark_v2")
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Messbestand", entscheidung.Reason);
    }

    [Fact]
    public void Ein_echter_Sprung_wie_vom_Mehrklassen_zum_BCC_Modell_wird_erlaubt()
    {
        // Mehrklassenmodell 13–20 % Recall gegen BCC-Einzelklasse 62–76 %.
        // Der Unterschied ist ein Vielfaches der Streuung — genau der Fall, fuer
        // den die Regel durchlassen muss.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Anteile([0.132, 0.153, 0.195], fehlalarm: [0.08, 0.19, 0.23]),
            Candidate = Anteile([0.622, 0.676, 0.757], fehlalarm: [0.03, 0.05, 0.07])
        });

        Assert.True(entscheidung.Promote);
        Assert.Equal(0.685, entscheidung.CandidateMean, 3);
    }

    [Fact]
    public void Ein_Kandidat_mit_hoeherer_interner_Zahl_aber_mehr_Fehlalarmen_wird_abgewiesen()
    {
        // bcc_bogen_af8020b688ac_v3_negatives hatte die hoechste interne mAP50
        // (0,9489) und feuerte auf 9 von 14 sauberen Negativbildern.
        // Der Recall-Gewinn ist hier bewusst gross genug, um die erste Sperre zu
        // passieren — sonst pruefte der Test die Fehlalarmregel gar nicht.
        var entscheidung = ModelPromotionPolicy.Decide(new ModelPromotionRequest
        {
            Incumbent = Anteile([0.62, 0.64, 0.66], fehlalarm: [0.03, 0.05, 0.07]),
            Candidate = Anteile([0.80, 0.81, 0.82], fehlalarm: [0.60, 0.64, 0.68])
        });

        Assert.False(entscheidung.Promote);
        Assert.Contains("Fehlalarm", entscheidung.Reason);
    }

    private static IReadOnlyList<ModelMeasurement> Von37(
        IReadOnlyList<int> treffer, string set = Set)
        => Anteile(treffer.Select(wert => wert / 37.0).ToList(), set: set);

    private static IReadOnlyList<ModelMeasurement> Anteile(
        IReadOnlyList<double> recall,
        IReadOnlyList<double>? fehlalarm = null,
        string set = Set)
        => recall
            .Select((wert, index) => new ModelMeasurement(
                set, Sha, 42 + index, wert, fehlalarm?[index] ?? 0.10))
            .ToList();
}
