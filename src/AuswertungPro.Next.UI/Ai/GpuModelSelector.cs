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
    /// <summary>Schnelles CLASSIFY-Modell (permanent geladen, ~3 GB + KV-Cache).</summary>
    public const string FastModel = "qwen3-vl:2b";

    /// <summary>Eskalationsmodell fuer Yellow-Zone (permanent geladen, ~6 GB + KV-Cache).</summary>
    public const string ReferenceModel = "qwen3-vl:8b";

    /// <summary>Laptop-Modell (nur 3B, kein Dual-Mode).</summary>
    public const string LaptopModel = "qwen3-vl:2b";

    /// <summary>VRAM-Schwelle in MB ab der Dual-Mode (3B + 8B permanent) aktiv ist.</summary>
    public const long DualModelThresholdMb = 16_000;

    /// <summary>VRAM-Schwelle in MB ab der Single-Mode (nur 3B) aktiv ist.</summary>
    public const long SingleModelThresholdMb = 6_000;

    /// <summary>NumCtx fuer das schnelle Modell (kompakt, wenig VRAM).</summary>
    public const int FastModelNumCtx = 4096;

    /// <summary>NumCtx fuer das Reference-Modell (mehr Kontext).</summary>
    public const int ReferenceModelNumCtx = 4096;

    /// <summary>
    /// Ergebnis der GPU-Erkennung.
    /// </summary>
    public sealed record GpuProfile(
        string ResolvedModel,
        int ResolvedNumCtx,
        long VramTotalMb,
        string GpuName,
        string Reason,
        string? ReferenceModel = null);

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
                LaptopModel, FastModelNumCtx, 0, "Unbekannt",
                "nvidia-smi nicht gefunden — verwende 8B als Fallback");

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

            if (vramMb >= DualModelThresholdMb)
            {
                return new GpuProfile(
                    FastModel, FastModelNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM — Dual-Mode (8B + 32B Fallback)",
                    ReferenceModel: ReferenceModel);
            }

            if (vramMb >= SingleModelThresholdMb)
            {
                return new GpuProfile(
                    LaptopModel, FastModelNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM — Single-Mode (nur 8B)");
            }

            return new GpuProfile(
                LaptopModel, FastModelNumCtx, vramMb, gpuName,
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
