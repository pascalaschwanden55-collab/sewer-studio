using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai.QualityGate;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TemperatureScalerTests
{
    [Fact]
    public void DefaultTemperature_IsIdentity()
    {
        var scaler = new TemperatureScaler();
        Assert.Equal(1.0, scaler.Temperature);

        // With T=1.0, Scale(x) ≈ x
        var scaled = scaler.Scale(0.7);
        Assert.InRange(scaled, 0.69, 0.71);
    }

    [Fact]
    public void Scale_ClampsExtremes()
    {
        var scaler = new TemperatureScaler();
        var low = scaler.Scale(0.001);
        var high = scaler.Scale(0.999);
        Assert.True(low >= 0 && low <= 1);
        Assert.True(high >= 0 && high <= 1);
    }

    [Fact]
    public void Fit_WellCalibrated_TemperatureNear1()
    {
        var scaler = new TemperatureScaler();
        var data = new List<(double Confidence, bool WasCorrect)>
        {
            (0.9, true), (0.9, true), (0.9, true), (0.9, false),
            (0.7, true), (0.7, true), (0.7, false),
            (0.5, true), (0.5, false),
            (0.3, false), (0.3, false), (0.3, true),
            (0.1, false), (0.1, false), (0.1, false), (0.1, true),
        };
        scaler.Fit(data);

        // For roughly calibrated data, T should be near 1.0
        Assert.InRange(scaler.Temperature, 0.3, 3.0);
    }

    [Fact]
    public void Fit_OverconfidentModel_TemperatureAbove1()
    {
        var scaler = new TemperatureScaler();
        var data = new List<(double Confidence, bool WasCorrect)>();
        // Model predicts 0.9 but is correct only 50% of the time
        for (int i = 0; i < 50; i++)
        {
            data.Add((0.9, i % 2 == 0));
        }
        scaler.Fit(data);

        // Over-confident model needs T > 1 to soften confidences
        Assert.True(scaler.Temperature > 1.0);
    }

    [Fact]
    public void Fit_TooFewSamples_NoChange()
    {
        var scaler = new TemperatureScaler();
        var data = new List<(double Confidence, bool WasCorrect)>
        {
            (0.9, true), (0.1, false)
        };
        scaler.Fit(data);

        // Not enough data → T stays at default
        Assert.Equal(1.0, scaler.Temperature);
    }
}
