using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace AuswertungPro.Next.UI.Services;

// SystemMonitorService GPU-Polling via nvidia-smi (parseable CSV) + Pfad-
// Resolution. Aus dem Hauptdatei extrahiert (Slice 12a).
public sealed partial class SystemMonitorService
{
    private void PollGpu()
    {
        if (!_gpuAvailable)
            return;

        // Query GPU less frequently (every other tick = ~4s) to reduce overhead
        if (_gpuQuerySkip++ % 2 != 0)
            return;

        Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _nvidiaSmiPath!,
                    Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu,clocks.current.graphics,name --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc is null) return;

                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                // Parse "82, 4521, 12288, 65, 1920, NVIDIA GeForce RTX 4070"
                var parts = output.Trim().Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 5)
                {
                    if (_gpuQuerySkip <= 4)
                        Log($"nvidia-smi: unerwartete Ausgabe: '{output.Trim()}'");
                    return;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gpuPct)) return;
                if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memUsed)) return;
                if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var memTotal)) return;
                // nvidia-smi may return "[N/A]" — keep last known value on parse failure
                bool hasTempC = int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tempC);
                bool hasClockMhz = int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clockMhz);
                var gpuName = parts.Length >= 6 ? parts[5].Trim() : "";

                _gpuFailCount = 0; // reset on success

                _dispatcher.BeginInvoke(() =>
                {
                    GpuPercent = gpuPct;
                    GpuMemUsedMb = memUsed;
                    GpuMemTotalMb = memTotal;
                    GpuMemPercent = memTotal > 0 ? (int)Math.Round(100.0 * memUsed / memTotal) : 0;
                    if (hasTempC)
                    {
                        GpuTempC = tempC;
                        IsGpuTempAvailable = true;
                    }
                    if (hasClockMhz)
                    {
                        GpuClockMhz = clockMhz;
                        IsGpuClockAvailable = true;
                    }
                    if (gpuName.Length > 0)
                        GpuName = gpuName;
                    IsGpuAvailable = true;
                });
            }
            catch (Exception ex)
            {
                // Only permanently disable after 5 consecutive failures
                if (++_gpuFailCount >= 5)
                {
                    _gpuAvailable = false;
                    Log($"nvidia-smi: deaktiviert nach 5 Fehlern ({ex.Message})");
                }
            }
        });
    }

    private static bool IsGpuHardwareType(HardwareType hardwareType)
        => hardwareType == HardwareType.GpuNvidia
           || hardwareType == HardwareType.GpuAmd
           || hardwareType == HardwareType.GpuIntel;

    private static string? FindNvidiaSmi()
    {
        // Check System32 first (modern NVIDIA drivers install here)
        var sys32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
        if (File.Exists(sys32))
            return sys32;

        // Legacy NVSMI folder
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvsmi = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (File.Exists(nvsmi))
            return nvsmi;

        // Try via NVIDIA driver folder (some installations)
        var nvidiaDriver = Path.Combine(programFiles, "NVIDIA Corporation", "NVIDIA NVS", "nvidia-smi.exe");
        if (File.Exists(nvidiaDriver))
            return nvidiaDriver;

        // Fallback: try from PATH
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0)
                    return "nvidia-smi";
            }
        }
        catch { /* not in PATH */ }

        return null;
    }
}
