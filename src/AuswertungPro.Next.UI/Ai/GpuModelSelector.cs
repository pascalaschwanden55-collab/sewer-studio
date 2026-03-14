using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Erkennt verfuegbaren GPU-VRAM via nvidia-smi und waehlt
/// automatisch das passende Qwen-Modell.
///
/// Profil-Logik:
///   >= 24 GB VRAM → qwen2.5vl:32b  (Workstation, ~26 GB bei Q5)
///   >=  8 GB VRAM → qwen2.5vl:7b   (Laptop, ~6 GB bei Q4)
///   sonst         → deaktiviert (kein Vision-Modell)
/// </summary>
public static class GpuModelSelector
{
    /// <summary>Modell fuer grosse GPUs (RTX 5090, 4090, A6000 etc.)</summary>
    public const string LargeModel = "qwen2.5vl:32b";

    /// <summary>Modell fuer kleinere GPUs (RTX 4070, 3060 12GB etc.)</summary>
    public const string SmallModel = "qwen2.5vl:7b";

    /// <summary>VRAM-Schwelle in MB ab der das grosse Modell verwendet wird.</summary>
    public const long LargeModelThresholdMb = 24_000;

    /// <summary>VRAM-Schwelle in MB ab der das kleine Modell verwendet wird.</summary>
    public const long SmallModelThresholdMb = 8_000;

    /// <summary>NumCtx fuer das grosse Modell.</summary>
    public const int LargeModelNumCtx = 8192;

    /// <summary>NumCtx fuer das kleine Modell (weniger RAM-Verbrauch).</summary>
    public const int SmallModelNumCtx = 4096;

    /// <summary>
    /// Ergebnis der GPU-Erkennung.
    /// </summary>
    public sealed record GpuProfile(
        string ResolvedModel,
        int ResolvedNumCtx,
        long VramTotalMb,
        string GpuName,
        string Reason);

    /// <summary>
    /// Prueft ob der uebergebene Modellname eine automatische Aufloesung erfordert.
    /// </summary>
    public static bool IsAutoMode(string? modelName)
        => string.IsNullOrWhiteSpace(modelName)
           || modelName.Equals("auto", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Erkennt GPU-VRAM und waehlt passendes Modell.
    /// Gibt null zurueck wenn nvidia-smi nicht verfuegbar ist.
    /// </summary>
    public static GpuProfile? DetectAndSelect()
    {
        var nvidiaSmi = FindNvidiaSmi();
        if (nvidiaSmi is null)
            return new GpuProfile(
                SmallModel, SmallModelNumCtx, 0, "Unbekannt",
                "nvidia-smi nicht gefunden — verwende kleines Modell als Fallback");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = nvidiaSmi,
                Arguments = "--query-gpu=memory.total,name --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            // Format: "32768, NVIDIA GeForce RTX 5090"
            var parts = output.Trim().Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 1)
                return null;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vramMb))
                return null;

            var gpuName = parts.Length >= 2 ? parts[1] : "NVIDIA GPU";

            if (vramMb >= LargeModelThresholdMb)
            {
                return new GpuProfile(
                    LargeModel, LargeModelNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM erkannt — verwende grosses Modell (32B)");
            }

            if (vramMb >= SmallModelThresholdMb)
            {
                return new GpuProfile(
                    SmallModel, SmallModelNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM erkannt — verwende kleines Modell (7B)");
            }

            return new GpuProfile(
                SmallModel, SmallModelNumCtx, vramMb, gpuName,
                $"GPU {gpuName} mit nur {vramMb} MB VRAM — KI-Vision evtl. eingeschraenkt");
        }
        catch
        {
            return null;
        }
    }

    // ── nvidia-smi Suche (gleiche Logik wie SystemMonitorService) ─────

    private static string? FindNvidiaSmi()
    {
        var sys32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
        if (File.Exists(sys32))
            return sys32;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvsmi = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (File.Exists(nvsmi))
            return nvsmi;

        // Fallback: PATH
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
        catch { /* nicht im PATH */ }

        return null;
    }
}
