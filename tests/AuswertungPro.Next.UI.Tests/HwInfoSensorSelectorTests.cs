using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HwInfoSensorSelectorTests
{
    [Theory]
    [InlineData(0u, 292u, false)]
    [InlineData(1u, 291u, false)]
    [InlineData(1u, 292u, true)]
    public void SharedMemoryLayout_VerhindertLesenUeberVerkuerzteElemente(
        uint count,
        uint elementSize,
        bool expected)
        => Assert.Equal(expected, HwInfoSharedMemoryReader.HasUsableLayout(count, elementSize));

    [Fact]
    public void Select_OrdnetTemperaturenUndTakteDenAnzeigenZu()
    {
        var readings = new[]
        {
            Reading(HwInfoReadingKind.Temperature, "CPU Package", 64.6),
            Reading(HwInfoReadingKind.Temperature, "GPU Temperature", 55.2),
            Reading(HwInfoReadingKind.Clock, "CPU Core Clock", 4899.7),
            Reading(HwInfoReadingKind.Clock, "GPU Graphics Clock", 1920.4),
            Reading(HwInfoReadingKind.Clock, "DRAM Clock", 3200.1)
        };

        var result = HwInfoSensorSelector.Select(readings);

        Assert.Equal(65, result.CpuTempC);
        Assert.Equal(55, result.GpuTempC);
        Assert.Equal(4900, result.CpuClockMhz);
        Assert.Equal(1920, result.GpuClockMhz);
        Assert.Equal(3200, result.RamClockMhz);
    }

    [Fact]
    public void Select_IgnoriertUngueltigeWerteUndBehältErstenTreffer()
    {
        var readings = new[]
        {
            Reading(HwInfoReadingKind.Temperature, "CPU Package", 0),
            Reading(HwInfoReadingKind.Temperature, "CPU Package", 61),
            Reading(HwInfoReadingKind.Temperature, "CPU Core", 72),
            Reading(HwInfoReadingKind.Temperature, "GPU Temperature", 151),
            Reading(HwInfoReadingKind.Clock, "DRAM Clock", -1)
        };

        var result = HwInfoSensorSelector.Select(readings);

        Assert.Equal(61, result.CpuTempC);
        Assert.Null(result.GpuTempC);
        Assert.Null(result.RamClockMhz);
    }

    [Fact]
    public void Select_VerwechseltGpuSpeichertaktNichtMitRamTakt()
    {
        var readings = new[]
        {
            Reading(HwInfoReadingKind.Clock, "GPU Memory Clock", 10500),
            Reading(HwInfoReadingKind.Clock, "Memory Clock", 3000)
        };

        var result = HwInfoSensorSelector.Select(readings);

        Assert.Equal(10500, result.GpuClockMhz);
        Assert.Equal(3000, result.RamClockMhz);
    }

    private static HwInfoRawReading Reading(HwInfoReadingKind kind, string label, double value)
        => new(kind, label, value);
}
