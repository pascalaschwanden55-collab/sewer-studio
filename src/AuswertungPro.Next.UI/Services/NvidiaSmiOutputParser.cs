using System;
using System.Globalization;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Analysiert die CSV-Textausgabe von nvidia-smi.
/// Reine Textauswertung, kein Prozess-Start, kein Systemzugriff.
/// </summary>
public static class NvidiaSmiOutputParser
{
    /// <summary>
    /// Analysiert eine einzelne CSV-Zeile von nvidia-smi
    /// (--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu,clocks.current.graphics,name --format=csv,noheader,nounits).
    /// </summary>
    /// <param name="output">Rohausgabe von nvidia-smi (eine Zeile).</param>
    /// <returns>Geparste Werte, oder null bei ungültigem Format (weniger als 5 Felder oder Pflichtfelder nicht parsebar).</returns>
    public static NvidiaSmiReading? Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var parts = output.Trim().Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 5)
            return null;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gpuPct))
            return null;
        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memUsed))
            return null;
        if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memTotal))
            return null;

        // nvidia-smi liefert u.U. "[N/A]" für Temp/Takt — optionale Felder
        bool hasTempC = int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tempC);
        bool hasClockMhz = int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clockMhz);

        var gpuName = parts.Length >= 6 ? parts[5].Trim() : string.Empty;

        return new NvidiaSmiReading(
            GpuPercent: gpuPct,
            MemUsedMb: memUsed,
            MemTotalMb: memTotal,
            TempC: hasTempC ? tempC : null,
            ClockMhz: hasClockMhz ? clockMhz : null,
            GpuName: gpuName);
    }

    /// <summary>
    /// Berechnet den Speicherauslastungsprozentsatz aus geparsten Rohmegabyte-Werten.
    /// </summary>
    public static int ComputeMemPercent(long memUsedMb, long memTotalMb)
        => memTotalMb > 0 ? (int)Math.Round(100.0 * memUsedMb / memTotalMb) : 0;
}

/// <summary>
/// Geparste Werte einer nvidia-smi-CSV-Ausgabe.
/// </summary>
/// <param name="GpuPercent">GPU-Auslastung in Prozent.</param>
/// <param name="MemUsedMb">Genutzter GPU-Speicher in MB.</param>
/// <param name="MemTotalMb">Gesamter GPU-Speicher in MB.</param>
/// <param name="TempC">GPU-Temperatur in °C (null wenn nicht verfügbar).</param>
/// <param name="ClockMhz">GPU-Kerntakt in MHz (null wenn nicht verfügbar).</param>
/// <param name="GpuName">GPU-Name (leer wenn nicht vorhanden).</param>
public sealed record NvidiaSmiReading(
    int GpuPercent,
    long MemUsedMb,
    long MemTotalMb,
    int? TempC,
    int? ClockMhz,
    string GpuName);
