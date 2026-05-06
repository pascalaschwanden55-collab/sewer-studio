using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Ueberwacht GPU-VRAM via nvidia-smi.
/// Wird vor SAM-Load aufgerufen um OOM zu verhindern.
///
/// VRAM-Budget RTX 5090 (32 GB):
///   Permanent: ~24.8 GB (YOLO + Qwen 8B×4×8192 + DINO + nomic + Overhead)
///   SAM on-demand: +3 GB
///   Reserve: ~4.2 GB
///
/// Sicherheitslogik:
///   CanLoadSam() → true wenn > MinFreeForSamMb frei
///   GetVramUsage() → aktueller VRAM-Verbrauch via nvidia-smi
/// </summary>
public sealed class VramMonitorService
{
    /// <summary>Minimum freies VRAM (MB) damit SAM geladen werden darf.</summary>
    public const long MinFreeForSamMb = 5_000;

    /// <summary>VRAM-Schwelle (MB) ab der eine Warnung geloggt wird.</summary>
    public const long WarningThresholdMb = 3_000;

    private readonly string? _nvidiaSmiPath;
    private long _lastUsedMb;
    private long _lastFreeMb;
    private long _lastTotalMb;
    private DateTime _lastCheck = DateTime.MinValue;

    /// <summary>Letzter bekannter VRAM-Verbrauch in MB.</summary>
    public long LastUsedMb => _lastUsedMb;
    /// <summary>Letztes bekanntes freies VRAM in MB.</summary>
    public long LastFreeMb => _lastFreeMb;
    /// <summary>Gesamtes VRAM in MB.</summary>
    public long TotalMb => _lastTotalMb;
    /// <summary>Zeitpunkt der letzten Messung.</summary>
    public DateTime LastCheckUtc => _lastCheck;

    public VramMonitorService()
    {
        _nvidiaSmiPath = FindNvidiaSmi();
    }

    /// <summary>
    /// Prueft ob genug VRAM frei ist um SAM zu laden (~3 GB).
    /// Fuehrt eine frische nvidia-smi Abfrage durch.
    /// </summary>
    public async Task<bool> CanLoadSamAsync(CancellationToken ct = default)
    {
        var usage = await GetVramUsageAsync(ct).ConfigureAwait(false);
        if (usage is null) return true; // nvidia-smi nicht verfuegbar → optimistisch

        var freeMb = usage.Value.TotalMb - usage.Value.UsedMb;

        if (freeMb < WarningThresholdMb)
        {
            Debug.WriteLine(
                $"[VramMonitor] WARNUNG: Nur {freeMb} MB VRAM frei — SAM-Load riskant!");
        }

        return freeMb >= MinFreeForSamMb;
    }

    /// <summary>
    /// Liest aktuellen VRAM-Verbrauch via nvidia-smi.
    /// Gibt null zurueck wenn nvidia-smi nicht verfuegbar.
    /// </summary>
    public async Task<(long UsedMb, long TotalMb)?> GetVramUsageAsync(CancellationToken ct = default)
    {
        if (_nvidiaSmiPath is null) return null;

        // Phase D2.3: ProcessRunner statt Process.Start direkt — sicherer ArgumentList
        // + asynchroner stdout/stderr-Drain + harter Timeout via Tree-Kill.
        var result = await ProcessRunner.RunAsync(
            fileName: _nvidiaSmiPath,
            arguments: ["--query-gpu=memory.used,memory.total", "--format=csv,noheader,nounits"],
            timeout: TimeSpan.FromSeconds(5),
            ct: ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            Debug.WriteLine($"[VramMonitor] nvidia-smi: {result.ToDiagnosticString()}");
            return null;
        }

        // Format: "24500, 32768"
        var parts = result.Stdout.Trim().Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return null;

        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var usedMb))
            return null;
        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalMb))
            return null;

        // Cache aktualisieren
        Interlocked.Exchange(ref _lastUsedMb, usedMb);
        Interlocked.Exchange(ref _lastFreeMb, totalMb - usedMb);
        Interlocked.Exchange(ref _lastTotalMb, totalMb);
        _lastCheck = DateTime.UtcNow;

        return (usedMb, totalMb);
    }

    /// <summary>
    /// Schneller Check ohne nvidia-smi Aufruf (benutzt gecachten Wert).
    /// Gibt true zurueck wenn der letzte Check weniger als maxAgeSec alt ist
    /// UND genug VRAM frei war.
    /// </summary>
    public bool CanLoadSamCached(int maxAgeSec = 10)
    {
        if (_lastCheck == DateTime.MinValue) return true; // Kein Check gemacht → optimistisch
        if ((DateTime.UtcNow - _lastCheck).TotalSeconds > maxAgeSec) return true; // Zu alt → optimistisch

        return _lastFreeMb >= MinFreeForSamMb;
    }

    // ── nvidia-smi Suche (identisch mit GpuModelSelector) ─────

    private static string? FindNvidiaSmi()
    {
        var sys32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
        if (File.Exists(sys32)) return sys32;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvsmi = Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        if (File.Exists(nvsmi)) return nvsmi;

        // Phase D2.3: PATH-Probe via ProcessRunner — synchron blockierender Aufruf
        // ist akzeptabel, da nur einmalig im Konstruktor (Sub-1s).
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
