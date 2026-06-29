using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierungstests für CpuDeltaCalculator (reine CPU-Delta-Mathematik).
/// </summary>
public sealed class CpuDeltaCalculatorTests
{
    [Fact]
    public void ComputePercent_NullWennDeltaTotalNull()
    {
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 0, deltaTotal: 0);
        Assert.Null(result);
    }

    [Fact]
    public void ComputePercent_NullWennDeltaTotalNegativ()
    {
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 100, deltaTotal: -1);
        Assert.Null(result);
    }

    [Fact]
    public void ComputePercent_NullProzentWennKeineBeschäftigung()
    {
        // Alle Ticks sind Idle → 0 % Auslastung
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 1000, deltaTotal: 1000);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ComputePercent_HundertProzentWennKeinIdle()
    {
        // Kein einziger Idle-Tick → 100 % Auslastung
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 0, deltaTotal: 1000);
        Assert.Equal(100, result);
    }

    [Fact]
    public void ComputePercent_FünfzigProzentBeiGleicherAufteilung()
    {
        // 500 idle von 1000 total = 50 %
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 500, deltaTotal: 1000);
        Assert.Equal(50, result);
    }

    [Fact]
    public void ComputePercent_RundetKorrektAuf()
    {
        // (1000 - 333) / 1000 = 66.7 % → rundet auf 67
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 333, deltaTotal: 1000);
        Assert.Equal(67, result);
    }

    [Fact]
    public void ComputePercent_TypischeLast_NeunzigProzent()
    {
        // 100 idle von 1000 total = 90 %
        var result = CpuDeltaCalculator.ComputePercent(deltaIdle: 100, deltaTotal: 1000);
        Assert.Equal(90, result);
    }
}
