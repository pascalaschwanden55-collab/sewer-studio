using System;
using System.Globalization;
using System.IO;
using System.Text;
using AuswertungPro.Next.Application.Common;
namespace AuswertungPro.Next.Infrastructure.Ai.Ollama;

/// <summary>
/// Erkennt verfuegbaren GPU-VRAM via nvidia-smi und waehlt
/// automatisch das passende Qwen-Modell.
///
/// Profil-Logik:
///   >= 24 GB VRAM → qwen3-vl:8b-q8  (Workstation, ~11.7 GB — laesst Platz fuer YOLO/DINO/SAM)
///   >=  8 GB VRAM → qwen3-vl:2b     (Laptop, ~2 GB)
///   sonst         → kleines Modell, KI-Vision evtl. eingeschraenkt
///
/// A/B Juni 2026: Qwen2.5-VL lieferte 0% (Parse-Fehler) — der Auto-Modus darf
/// NIE wieder still auf die 2.5-Familie zurueckfallen.
/// </summary>
public static class GpuModelSelector
{
    /// <summary>Modell fuer grosse GPUs (RTX 5090, 4090, A6000 etc.).
    /// Bewusst 8B-Q8 statt 32B: das 32B laeuft nur als RAM-Referenz, und auf der GPU
    /// muss neben dem VLM der Sidecar-Stack (YOLO/DINO/SAM) Platz haben (VRAM-Budget 29 GB).</summary>
    public const string LargeModel = "qwen3-vl:8b-q8";

    /// <summary>Modell fuer kleinere GPUs (RTX 4070, 3060 12GB etc.)</summary>
    public const string SmallModel = "qwen3-vl:2b";

    /// <summary>VRAM-Schwelle in MB ab der das grosse Modell verwendet wird.</summary>
    public const long LargeModelThresholdMb = 24_000;

    /// <summary>VRAM-Schwelle in MB ab der das kleine Modell verwendet wird.</summary>
    public const long SmallModelThresholdMb = 8_000;

    /// <summary>NumCtx fuer das grosse Modell.</summary>
    public const int LargeModelNumCtx = 12288;

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
            var result = ExternalProcessRunner.RunAsync(
                nvidiaSmi,
                ["--query-gpu=memory.total,name", "--format=csv,noheader,nounits"],
                TimeSpan.FromSeconds(5),
                Encoding.UTF8,
                Encoding.UTF8).GetAwaiter().GetResult();
            if (!result.Success)
                return null;

            var output = result.StdOut;

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
                    $"GPU {gpuName} mit {vramMb} MB VRAM erkannt — verwende grosses Modell ({LargeModel})");
            }

            if (vramMb >= SmallModelThresholdMb)
            {
                return new GpuProfile(
                    SmallModel, SmallModelNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM erkannt — verwende kleines Modell ({SmallModel})");
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
            var result = ExternalProcessRunner.RunAsync(
                "nvidia-smi",
                ["--version"],
                TimeSpan.FromSeconds(3),
                Encoding.UTF8,
                Encoding.UTF8).GetAwaiter().GetResult();
            if (result.Success)
                return "nvidia-smi";
        }
        catch { /* nicht im PATH */ }

        return null;
    }
}
