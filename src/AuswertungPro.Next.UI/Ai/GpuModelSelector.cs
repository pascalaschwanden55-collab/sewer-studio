using System;
using AuswertungPro.Next.Application.Ai.Ollama;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Erkennt verfuegbaren GPU-VRAM via nvidia-smi und waehlt
/// automatisch das passende Qwen-Modell.
///
/// V4.1 Profil-Logik (2-Tier, kein Laptop):
///   >= 24 GB VRAM → 8B×6 Parallel, 8192 ctx, Batch-Pipeline, alles permanent  (Workstation)
///   >= 12 GB VRAM → 8B×2 Parallel, 8192 ctx                                   (Desktop)
///   sonst         → 8B×1, 8192 ctx (Minimal)
///
/// 8B ist das primaere Vision-Modell. 32B als Swap-Eskalation bei Yellow/Red.
/// Batch-Pipeline: YOLO→DINO→SAM batched, dann Qwen ×6 parallel.
/// </summary>
public static class GpuModelSelector
{
    /// <summary>Einziges Vision-Modell (8B, alle Tiers).</summary>
    public const string PrimaryModel = "qwen3-vl:8b";

    // ── Backward-Compat Aliases ──
    public const string FastModel = PrimaryModel;
    public const string ReferenceModel = PrimaryModel;
    public const string WorkstationModel = PrimaryModel;
    public const string DesktopModel = PrimaryModel;
    public const string LaptopModel = PrimaryModel;

    /// <summary>VRAM-Schwelle fuer Workstation (8B×4 + DINO perm. + SAM on-demand).</summary>
    public const long WorkstationThresholdMb = 24_000;

    /// <summary>VRAM-Schwelle fuer Desktop (8B×2).</summary>
    public const long DesktopThresholdMb = 12_000;

    // Backward-Compat Aliases
    public const long DualModelThresholdMb = DesktopThresholdMb;
    public const long SingleModelThresholdMb = 6_000;
    public const long LaptopThresholdMb = SingleModelThresholdMb;

    /// <summary>NumCtx: 8192 fuer alle Tiers.</summary>
    public const int DefaultNumCtx = 8192;

    // Backward-Compat Aliases
    public const int WorkstationNumCtx = DefaultNumCtx;
    public const int DesktopNumCtx = DefaultNumCtx;
    public const int LaptopNumCtx = DefaultNumCtx;
    public const int FastModelNumCtx = DefaultNumCtx;
    public const int ReferenceModelNumCtx = DefaultNumCtx;

    /// <summary>
    /// Ergebnis der GPU-Erkennung.
    /// </summary>
    public sealed record GpuProfile(
        string ResolvedModel,
        int ResolvedNumCtx,
        long VramTotalMb,
        string GpuName,
        string Reason,
        string? ReferenceModel = null,
        int ParallelSlots = 1,
        bool DinoPermanent = false);

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
                PrimaryModel, DefaultNumCtx, 0, "Unbekannt",
                "nvidia-smi nicht gefunden — verwende 8B als Fallback");

        try
        {
            // Phase D2.3: ProcessRunner statt direktem Process.Start.
            // GetAwaiter().GetResult() ist akzeptabel — DetectAndSelect laeuft nur
            // einmal beim App-Start (< 1 s) und Aufrufer ist synchron.
            var result = ProcessRunner.RunAsync(
                fileName: nvidiaSmi,
                arguments: ["--query-gpu=memory.total,name", "--format=csv,noheader,nounits"],
                timeout: TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

            if (!result.IsSuccess)
                return null;

            // Format: "32768, NVIDIA GeForce RTX 5090"
            var parts = result.Stdout.Trim().Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 1)
                return null;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vramMb))
                return null;

            var gpuName = parts.Length >= 2 ? parts[1] : "NVIDIA GPU";

            // Tier 1: Workstation (RTX 5090 etc.)
            // 8B×6 Slots, 8192 ctx, Batch-Pipeline, alles permanent
            // VRAM (Flash Attn): 8B(8.1GB) + Sidecar(~10GB) + nomic(0.6GB) ≈ 20GB, ~12GB Reserve
            if (vramMb >= WorkstationThresholdMb)
            {
                return new GpuProfile(
                    PrimaryModel, DefaultNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM — Workstation (8B×6 Batch-Pipeline, 8192ctx)",
                    ReferenceModel: OllamaConfig.DefaultReferenceVisionModel,
                    ParallelSlots: 6,
                    DinoPermanent: true);
            }

            // Tier 2: Desktop (RTX 3090/4080 etc.)
            // 8B×2 Slots, 8192 ctx
            if (vramMb >= DesktopThresholdMb)
            {
                return new GpuProfile(
                    PrimaryModel, DefaultNumCtx, vramMb, gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM — Desktop (8B×2, 8192ctx)",
                    ParallelSlots: 2);
            }

            // Minimal: 8B×1
            return new GpuProfile(
                PrimaryModel, DefaultNumCtx, vramMb, gpuName,
                $"GPU {gpuName} mit {vramMb} MB VRAM — Minimal (8B×1, 8192ctx)",
                ParallelSlots: 1);
        }
        catch
        {
            return null;
        }
    }

    // ── nvidia-smi Suche ─────────────────────────────────────────────

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

        // Fallback: PATH-Probe via ProcessRunner.
        try
        {
            var result = ProcessRunner.RunAsync(
                fileName: "nvidia-smi",
                arguments: ["--version"],
                timeout: TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            if (result.IsSuccess) return "nvidia-smi";
        }
        catch { /* nicht im PATH */ }

        return null;
    }
}
