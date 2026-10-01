using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaFindingClockResolverTests
{
    [Fact]
    public void Resolve_rejects_old_WinCan_meter_mirror()
    {
        var finding = new VsaFinding
        {
            MeterStart = 3,
            MeterEnd = 3,
            SchadenlageAnfang = 3,
            SchadenlageEnde = 3
        };

        Assert.Equal(default, VsaFindingClockResolver.Resolve(finding));
    }

    [Fact]
    public void Resolve_keeps_valid_start_only_clock_even_at_same_meter()
    {
        var finding = new VsaFinding
        {
            MeterStart = 9,
            SchadenlageAnfang = 9
        };

        Assert.Equal(
            new VsaFindingClockPositions(9, null),
            VsaFindingClockResolver.Resolve(finding));
    }

    [Fact]
    public void Resolve_rejects_non_integer_meter_value()
    {
        var finding = new VsaFinding
        {
            SchadenlageAnfang = 2.62136
        };

        Assert.Equal(default, VsaFindingClockResolver.Resolve(finding));
    }

    [Fact]
    public void Resolve_keeps_valid_clock_range()
    {
        var finding = new VsaFinding
        {
            MeterStart = 2.6,
            SchadenlageAnfang = 4,
            SchadenlageEnde = 8
        };

        Assert.Equal(
            new VsaFindingClockPositions(4, 8),
            VsaFindingClockResolver.Resolve(finding));
    }
}
