using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="MarkerColorClassifier"/>.
/// Verifiziert die QualityGate-Schwellen-Logik isoliert von WPF.
/// </summary>
public sealed class MarkerColorClassifierTests
{
    // ═══ Abgelehnt hat Prioritaet ═══

    [Fact]
    public void Rejected_liefert_Rejected_unabhaengig_von_Konfidenz()
    {
        Assert.Equal(MarkerZone.Rejected, MarkerColorClassifier.Classify(0.99, isRejected: true));
        Assert.Equal(MarkerZone.Rejected, MarkerColorClassifier.Classify(0.00, isRejected: true));
        Assert.Equal(MarkerZone.Rejected, MarkerColorClassifier.Classify(-1.0, isRejected: true));
    }

    // ═══ Kein KI-Kontext (manuell) ═══

    [Fact]
    public void Negative_Konfidenz_ohne_Rejected_liefert_Manual()
    {
        Assert.Equal(MarkerZone.Manual, MarkerColorClassifier.Classify(-1.0, isRejected: false));
        Assert.Equal(MarkerZone.Manual, MarkerColorClassifier.Classify(-0.001, isRejected: false));
    }

    // ═══ Gruen-Schwelle (>= 0.85) ═══

    [Theory]
    [InlineData(0.85)]
    [InlineData(1.00)]
    [InlineData(0.90)]
    public void Konfidenz_ab_Schwelle_Green_liefert_Green(double conf)
        => Assert.Equal(MarkerZone.Green, MarkerColorClassifier.Classify(conf, isRejected: false));

    [Fact]
    public void Knapp_unter_Green_Schwelle_liefert_Yellow()
        => Assert.Equal(MarkerZone.Yellow, MarkerColorClassifier.Classify(0.84, isRejected: false));

    // ═══ Gelb-Schwelle (>= 0.60, < 0.85) ═══

    [Theory]
    [InlineData(0.60)]
    [InlineData(0.75)]
    [InlineData(0.84)]
    public void Konfidenz_zwischen_Yellow_und_Green_liefert_Yellow(double conf)
        => Assert.Equal(MarkerZone.Yellow, MarkerColorClassifier.Classify(conf, isRejected: false));

    [Fact]
    public void Knapp_unter_Yellow_Schwelle_liefert_Red()
        => Assert.Equal(MarkerZone.Red, MarkerColorClassifier.Classify(0.59, isRejected: false));

    // ═══ Rot (< 0.60) ═══

    [Theory]
    [InlineData(0.00)]
    [InlineData(0.30)]
    [InlineData(0.59)]
    public void Konfidenz_unter_Yellow_liefert_Red(double conf)
        => Assert.Equal(MarkerZone.Red, MarkerColorClassifier.Classify(conf, isRejected: false));

    // ═══ Schwellenwert-Konstanten ═══

    [Fact]
    public void Schwellen_konstanten_stimmen_mit_Erwartung_ueberein()
    {
        Assert.Equal(0.85, MarkerColorClassifier.ThresholdGreen);
        Assert.Equal(0.60, MarkerColorClassifier.ThresholdYellow);
    }
}
