using System.IO.MemoryMappedFiles;
using System.Text;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Liest die HWiNFO-Sensortabelle. Auswahl und Anzeige der Messwerte bleiben
/// ausserhalb, damit das binaere Dateiformat nicht den Systemmonitor aufblaeht.
/// </summary>
internal static class HwInfoSharedMemoryReader
{
    private const string SensorsMapName = "Global\\HWiNFO_SENS_SM2";
    private const uint ExpectedSignature = 0x53695748; // "HWiS", little-endian
    private const int TemperatureType = 1;
    private const int ClockType = 6;
    private const uint MinimumReadingElementSize = 292; // Wert bei Offset 284, 8 Byte double

    public static HwInfoReadResult Read()
    {
        using var mmf = MemoryMappedFile.OpenExisting(SensorsMapName, MemoryMappedFileRights.Read);
        using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        if (accessor.ReadUInt32(0) != ExpectedSignature)
            return HwInfoReadResult.InvalidSignature();

        var offsetReadings = accessor.ReadUInt32(32);
        var sizeReading = accessor.ReadUInt32(36);
        var numReadings = accessor.ReadUInt32(40);
        if (!HasUsableLayout(numReadings, sizeReading))
            return HwInfoReadResult.NoData();

        var readings = new List<HwInfoRawReading>();
        var labelBytes = new byte[128];
        for (uint index = 0; index < numReadings; index++)
        {
            var position = offsetReadings + (long)index * sizeReading;
            var readingType = accessor.ReadInt32(position);
            if (readingType is not (TemperatureType or ClockType))
                continue;

            Array.Clear(labelBytes);
            accessor.ReadArray(position + 12, labelBytes, 0, labelBytes.Length);
            var label = Encoding.ASCII.GetString(labelBytes).TrimEnd('\0');
            var value = accessor.ReadDouble(position + 284);
            readings.Add(new HwInfoRawReading(
                readingType == TemperatureType
                    ? HwInfoReadingKind.Temperature
                    : HwInfoReadingKind.Clock,
                label,
                value));
        }

        return HwInfoReadResult.Active(numReadings, readings);
    }

    internal static bool HasUsableLayout(uint readingCount, uint readingElementSize)
        => readingCount > 0 && readingElementSize >= MinimumReadingElementSize;
}

internal enum HwInfoReadStatus
{
    Active,
    NoData,
    InvalidSignature
}

internal enum HwInfoReadingKind
{
    Temperature,
    Clock
}

internal sealed record HwInfoRawReading(HwInfoReadingKind Kind, string Label, double Value);

internal sealed record HwInfoReadResult(
    HwInfoReadStatus Status,
    uint TotalReadingCount,
    IReadOnlyList<HwInfoRawReading> Readings)
{
    public static HwInfoReadResult Active(uint count, IReadOnlyList<HwInfoRawReading> readings)
        => new(HwInfoReadStatus.Active, count, readings);

    public static HwInfoReadResult NoData()
        => new(HwInfoReadStatus.NoData, 0, Array.Empty<HwInfoRawReading>());

    public static HwInfoReadResult InvalidSignature()
        => new(HwInfoReadStatus.InvalidSignature, 0, Array.Empty<HwInfoRawReading>());
}

/// <summary>Ordnet HWiNFO-Beschriftungen den Anzeigen im Systemmonitor zu.</summary>
internal static class HwInfoSensorSelector
{
    public static HwInfoSensorSnapshot Select(IEnumerable<HwInfoRawReading> readings)
    {
        int? cpuTemp = null;
        int? gpuTemp = null;
        int? cpuClock = null;
        int? gpuClock = null;
        int? ramClock = null;

        foreach (var reading in readings)
        {
            var label = reading.Label.ToLowerInvariant();
            if (reading.Kind == HwInfoReadingKind.Temperature)
            {
                var tempC = (int)Math.Round(reading.Value);
                if (tempC is <= 0 or >= 150)
                    continue;

                if (!cpuTemp.HasValue && IsCpuLabel(label))
                    cpuTemp = tempC;
                else if (!gpuTemp.HasValue && IsGpuLabel(label))
                    gpuTemp = tempC;
            }
            else
            {
                var clockMhz = (int)Math.Round(reading.Value);
                if (clockMhz <= 0)
                    continue;

                if (!cpuClock.HasValue && IsCpuClockLabel(label))
                    cpuClock = clockMhz;
                else if (!gpuClock.HasValue && IsGpuLabel(label))
                    gpuClock = clockMhz;
                else if (!ramClock.HasValue && IsRamClockLabel(label))
                    ramClock = clockMhz;
            }
        }

        return new HwInfoSensorSnapshot(cpuTemp, gpuTemp, cpuClock, gpuClock, ramClock);
    }

    private static bool IsCpuLabel(string label)
        => label.Contains("cpu") || label.Contains("package") || label.Contains("core")
           || label.Contains("tctl") || label.Contains("die");

    private static bool IsCpuClockLabel(string label)
        => label.Contains("cpu") || label.Contains("core");

    private static bool IsGpuLabel(string label)
        => label.Contains("gpu") || label.Contains("graphics");

    private static bool IsRamClockLabel(string label)
        => !label.Contains("gpu") && (label.Contains("memory") || label.Contains("dram"));
}

internal sealed record HwInfoSensorSnapshot(
    int? CpuTempC,
    int? GpuTempC,
    int? CpuClockMhz,
    int? GpuClockMhz,
    int? RamClockMhz);
