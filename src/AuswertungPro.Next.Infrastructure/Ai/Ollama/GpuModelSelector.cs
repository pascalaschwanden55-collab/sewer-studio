using System.Globalization;
using System.Text;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Ollama;

public interface IGpuModelSelector
{
    GpuModelSelector.GpuProfile? DetectAndSelect();
}

/// <summary>Erkennt NVIDIA-GPUs und waehlt das passende lokale Modell.</summary>
public sealed class GpuModelSelectionService : IGpuModelSelector
{
    private readonly Func<string, bool> _fileExists;
    private readonly Func<Environment.SpecialFolder, string> _getFolderPath;
    private readonly Func<string, IReadOnlyList<string>, TimeSpan, ExternalProcessRunResult> _runProcess;

    public GpuModelSelectionService()
        : this(File.Exists, Environment.GetFolderPath, RunProcess)
    {
    }

    public GpuModelSelectionService(
        Func<string, bool> fileExists,
        Func<Environment.SpecialFolder, string> getFolderPath,
        Func<string, IReadOnlyList<string>, TimeSpan, ExternalProcessRunResult> runProcess)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _getFolderPath = getFolderPath ?? throw new ArgumentNullException(nameof(getFolderPath));
        _runProcess = runProcess ?? throw new ArgumentNullException(nameof(runProcess));
    }

    public GpuModelSelector.GpuProfile? DetectAndSelect()
    {
        var nvidiaSmi = FindNvidiaSmi();
        if (nvidiaSmi is null)
        {
            return new GpuModelSelector.GpuProfile(
                GpuModelSelector.SmallModel,
                GpuModelSelector.SmallModelNumCtx,
                0,
                "Unbekannt",
                "nvidia-smi nicht gefunden — verwende kleines Modell als Fallback");
        }

        try
        {
            var result = _runProcess(
                nvidiaSmi,
                ["--query-gpu=memory.total,name", "--format=csv,noheader,nounits"],
                TimeSpan.FromSeconds(5));
            if (!result.Success)
                return null;

            var parts = result.StdOut.Trim().Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 1)
                return null;

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var vramMb))
                return null;

            var gpuName = parts.Length >= 2 ? parts[1] : "NVIDIA GPU";

            if (vramMb >= GpuModelSelector.LargeModelThresholdMb)
            {
                return new GpuModelSelector.GpuProfile(
                    GpuModelSelector.LargeModel,
                    GpuModelSelector.LargeModelNumCtx,
                    vramMb,
                    gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM erkannt — verwende grosses Modell ({GpuModelSelector.LargeModel})");
            }

            if (vramMb >= GpuModelSelector.SmallModelThresholdMb)
            {
                return new GpuModelSelector.GpuProfile(
                    GpuModelSelector.SmallModel,
                    GpuModelSelector.SmallModelNumCtx,
                    vramMb,
                    gpuName,
                    $"GPU {gpuName} mit {vramMb} MB VRAM erkannt — verwende kleines Modell ({GpuModelSelector.SmallModel})");
            }

            return new GpuModelSelector.GpuProfile(
                GpuModelSelector.SmallModel,
                GpuModelSelector.SmallModelNumCtx,
                vramMb,
                gpuName,
                $"GPU {gpuName} mit nur {vramMb} MB VRAM — KI-Vision evtl. eingeschraenkt");
        }
        catch
        {
            return null;
        }
    }

    private string? FindNvidiaSmi()
    {
        var sys32 = Path.Combine(
            _getFolderPath(Environment.SpecialFolder.System),
            "nvidia-smi.exe");
        if (_fileExists(sys32))
            return sys32;

        var programFiles = _getFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvsmi = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (_fileExists(nvsmi))
            return nvsmi;

        try
        {
            var result = _runProcess(
                "nvidia-smi",
                ["--version"],
                TimeSpan.FromSeconds(3));
            if (result.Success)
                return "nvidia-smi";
        }
        catch
        {
            // Nicht im PATH.
        }

        return null;
    }

    private static ExternalProcessRunResult RunProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout) =>
        ExternalProcessRunner.RunAsync(
            fileName,
            arguments,
            timeout,
            Encoding.UTF8,
            Encoding.UTF8).GetAwaiter().GetResult();
}

/// <summary>
/// Erkennt verfuegbaren GPU-VRAM via nvidia-smi und waehlt automatisch das
/// passende Qwen-Modell. Die Hardware-Erkennung liegt im Instanzdienst; diese
/// Klasse behaelt die oeffentliche API und die reinen Modellregeln.
/// </summary>
public static class GpuModelSelector
{
    private static readonly IGpuModelSelector Default = new GpuModelSelectionService();

    /// <summary>Modell fuer grosse GPUs mit Platz fuer den Sidecar-Stack.</summary>
    public const string LargeModel = "qwen3-vl:8b-q8";

    /// <summary>Modell fuer kleinere GPUs.</summary>
    public const string SmallModel = "qwen3-vl:2b";

    /// <summary>VRAM-Schwelle in MB fuer das grosse Modell.</summary>
    public const long LargeModelThresholdMb = 24_000;

    /// <summary>VRAM-Schwelle in MB fuer das kleine Modell.</summary>
    public const long SmallModelThresholdMb = 8_000;

    /// <summary>Kontextgroesse fuer das grosse Modell.</summary>
    public const int LargeModelNumCtx = 12288;

    /// <summary>Kontextgroesse fuer das kleine Modell.</summary>
    public const int SmallModelNumCtx = 4096;

    /// <summary>Ergebnis der GPU-Erkennung und Modellwahl.</summary>
    public sealed record GpuProfile(
        string ResolvedModel,
        int ResolvedNumCtx,
        long VramTotalMb,
        string GpuName,
        string Reason);

    public static IGpuModelSelector Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IGpuModelSelector selector) =>
        throw new NotSupportedException(
            "Die globale GPU-Modellwahl kann nicht mehr ausgetauscht werden. " +
            "IGpuModelSelector bitte per Konstruktor uebergeben.");

    /// <summary>Prueft, ob der Modellname automatisch aufgeloest werden soll.</summary>
    public static bool IsAutoMode(string? modelName)
        => string.IsNullOrWhiteSpace(modelName)
           || modelName.Equals("auto", StringComparison.OrdinalIgnoreCase);

    public static GpuProfile? DetectAndSelect() => Current.DetectAndSelect();
}
