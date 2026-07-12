using AuswertungPro.Next.UI.Services;
using LibreHardwareMonitor.Hardware;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LibreHardwareMonitorSensorTests
{
    [Fact]
    public void Select_bevorzugt_CPU_Package_und_liefert_Taktwerte()
    {
        var reading = LibreHardwareMonitorSensor.Select([
            Sample(HardwareType.Cpu, SensorType.Temperature, "CPU Core #1", 72),
            Sample(HardwareType.Cpu, SensorType.Temperature, "CPU Package", 65),
            Sample(HardwareType.Cpu, SensorType.Clock, "Bus Speed", 100),
            Sample(HardwareType.Cpu, SensorType.Clock, "CPU Core #1", 4_200),
            Sample(HardwareType.Cpu, SensorType.Clock, "CPU Core #2", 4_350),
            Sample(HardwareType.Memory, SensorType.Clock, "Memory Clock", 3_200),
            Sample(HardwareType.Memory, SensorType.Temperature, "DIMM", 46)
        ]);

        Assert.Equal(65, reading.CpuTempC);
        Assert.Equal(4_350, reading.CpuClockMhz);
        Assert.Equal(3_200, reading.RamClockMhz);
        Assert.Equal(46, reading.RamTempC);
    }

    [Fact]
    public void Select_nutzt_Mainboard_als_Temperatur_Fallback()
    {
        var reading = LibreHardwareMonitorSensor.Select([
            Sample(HardwareType.Motherboard, SensorType.Temperature, "CPU Socket", 54),
            Sample(HardwareType.SuperIO, SensorType.Temperature, "CPU Package", 61),
            Sample(HardwareType.SuperIO, SensorType.Temperature, "DIMM 1", 43),
            Sample(HardwareType.SuperIO, SensorType.Temperature, "DIMM ungueltig", 170)
        ]);

        Assert.Equal(61, reading.CpuTempC);
        Assert.Equal(43, reading.RamTempC);
    }

    [Fact]
    public void Select_behaelt_die_bisherige_GPU_Sensorprioritaet()
    {
        var reading = LibreHardwareMonitorSensor.Select([
            Sample(HardwareType.GpuNvidia, SensorType.Load, "Memory Controller", 90, "RTX Test"),
            Sample(HardwareType.GpuNvidia, SensorType.Load, "GPU Core", 41, "RTX Test"),
            Sample(HardwareType.GpuNvidia, SensorType.Clock, "Memory", 9_000, "RTX Test"),
            Sample(HardwareType.GpuNvidia, SensorType.Clock, "Graphics", 2_100, "RTX Test"),
            Sample(HardwareType.GpuNvidia, SensorType.Temperature, "GPU Core", 62, "RTX Test"),
            Sample(HardwareType.GpuNvidia, SensorType.Temperature, "GPU Hot Spot", 78, "RTX Test")
        ]);

        Assert.Equal("RTX Test", reading.GpuName);
        Assert.Equal(41, reading.GpuLoadPercent);
        Assert.Equal(2_100, reading.GpuClockMhz);
        Assert.Equal(78, reading.GpuTempC);
    }

    [Fact]
    public void Deaktivierter_Sensor_startet_keine_native_Hardwareabfrage()
    {
        using var sensor = new LibreHardwareMonitorSensor(enabled: false);

        var initialization = sensor.Initialize();
        var poll = sensor.Poll();

        Assert.False(initialization.Succeeded);
        Assert.Empty(initialization.Messages);
        Assert.Null(poll.Reading);
        Assert.False(poll.RetryRequested);
    }

    private static LibreHardwareSensorSample Sample(
        HardwareType hardwareType,
        SensorType sensorType,
        string sensorName,
        float value,
        string hardwareName = "Testhardware")
        => new(hardwareType, hardwareName, sensorType, sensorName, value);
}
