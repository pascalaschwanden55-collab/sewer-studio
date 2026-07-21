using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ConfidenceBandClassifierTests
{
    [Theory]
    [InlineData(double.NegativeInfinity, ConfidenceBand.Missing)]
    [InlineData(-0.001, ConfidenceBand.Missing)]
    [InlineData(0.0, ConfidenceBand.Low)]
    [InlineData(0.59, ConfidenceBand.Low)]
    [InlineData(0.60, ConfidenceBand.Medium)]
    [InlineData(0.849999, ConfidenceBand.Medium)]
    [InlineData(0.85, ConfidenceBand.High)]
    [InlineData(1.0, ConfidenceBand.High)]
    [InlineData(double.PositiveInfinity, ConfidenceBand.High)]
    public void Classify_erhaelt_die_bestehenden_Konfidenzbereiche(
        double confidence,
        ConfidenceBand expected)
        => Assert.Equal(expected, ConfidenceBandClassifier.Classify(confidence));

    [Fact]
    public void Classify_behandelt_NaN_wie_bisher_als_niedrige_Konfidenz()
        => Assert.Equal(ConfidenceBand.Low, ConfidenceBandClassifier.Classify(double.NaN));

    [Fact]
    public void Thresholds_sind_die_bestehenden_Anzeigegrenzen()
    {
        Assert.Equal(0.85, ConfidenceBandClassifier.HighThreshold);
        Assert.Equal(0.60, ConfidenceBandClassifier.MediumThreshold);
    }
}
