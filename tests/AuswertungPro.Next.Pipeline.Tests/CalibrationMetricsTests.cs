using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Ai.QualityGate;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CalibrationMetricsTests
{
    [Fact]
    public void WellCalibrated_EceIsLow()
    {
        // Create data where accuracy in each bin matches the confidence
        var data = new List<(double Confidence, bool WasCorrect)>();
        var rng = new System.Random(42);

        // For each bin center, create samples whose accuracy matches the confidence
        for (int bin = 0; bin < 10; bin++)
        {
            double conf = (bin + 0.5) / 10.0; // bin centers: 0.05, 0.15, ..., 0.95
            int samplesPerBin = 20;
            int correctCount = (int)(conf * samplesPerBin);
            for (int i = 0; i < samplesPerBin; i++)
                data.Add((conf, i < correctCount));
        }

        var ece = CalibrationMetrics.ComputeEce(data);
        // Well-calibrated → ECE close to 0
        Assert.InRange(ece, 0, 0.05);
    }

    [Fact]
    public void CompletelyMiscalibrated_EceIsHigh()
    {
        var data = new List<(double Confidence, bool WasCorrect)>();
        // High confidence, always wrong
        for (int i = 0; i < 50; i++)
            data.Add((0.95, false));
        // Low confidence, always correct
        for (int i = 0; i < 50; i++)
            data.Add((0.05, true));

        var ece = CalibrationMetrics.ComputeEce(data);
        Assert.True(ece > 0.8);
    }

    [Fact]
    public void EmptyData_ReturnsZero()
    {
        var ece = CalibrationMetrics.ComputeEce(new List<(double, bool)>());
        Assert.Equal(0, ece);
    }

    [Fact]
    public void BinDetails_Returns10Bins()
    {
        var data = new List<(double Confidence, bool WasCorrect)>
        {
            (0.1, false), (0.3, true), (0.5, true), (0.7, true), (0.9, true)
        };

        var bins = CalibrationMetrics.GetBinDetails(data);
        Assert.Equal(10, bins.Count);
        Assert.Equal(5, bins.Sum(b => b.SampleCount));
    }

    [Fact]
    public void BinBoundaries_AreCorrect()
    {
        var data = new List<(double Confidence, bool WasCorrect)> { (0.5, true) };
        var bins = CalibrationMetrics.GetBinDetails(data);

        Assert.Equal(0.0, bins[0].BinLower);
        Assert.Equal(0.1, bins[0].BinUpper);
        Assert.Equal(0.9, bins[9].BinLower);
        Assert.Equal(1.0, bins[9].BinUpper);
    }
}
