// AuswertungPro – Video-Selbsttraining Phase 4
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Speichert und verwaltet das Benchmark-Set (10-20 geschuetzte Haltungen).
/// Benchmark-Haltungen werden NIE in die KB aufgenommen und dienen als
/// unveraenderlicher Goldstandard fuer die Fortschrittsmessung.
/// </summary>
public sealed class BenchmarkSetStore
{
    private static string FilePath => Path.Combine(
        KnowledgeRoot.GetRoot(), "benchmark_set.json");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<BenchmarkSet> LoadAsync(CancellationToken ct = default)
    {
        var path = FilePath;
        if (!File.Exists(path))
            return new BenchmarkSet();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<BenchmarkSet>(json, JsonOpts) ?? new BenchmarkSet();
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(BenchmarkSet set, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Atomares Speichern: temp → validate → move
            var tmp = path + ".tmp";
            var json = JsonSerializer.Serialize(set, JsonOpts);
            await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);

            // Validierung: JSON muss deserialisierbar sein
            var check = JsonSerializer.Deserialize<BenchmarkSet>(json, JsonOpts);
            if (check?.Haltungen is null)
                throw new InvalidOperationException("Benchmark-Set Validierung fehlgeschlagen.");

            File.Move(tmp, path, overwrite: true);
        }
        finally { _lock.Release(); }
    }

    public async Task AddHaltungAsync(BenchmarkHaltung haltung, CancellationToken ct = default)
    {
        var set = await LoadAsync(ct).ConfigureAwait(false);
        set.Haltungen.Add(haltung);
        await SaveAsync(set, ct).ConfigureAwait(false);
    }

    public async Task RemoveHaltungAsync(string haltungId, CancellationToken ct = default)
    {
        var set = await LoadAsync(ct).ConfigureAwait(false);
        set.Haltungen.RemoveAll(h => h.HaltungId == haltungId);
        await SaveAsync(set, ct).ConfigureAwait(false);
    }

    /// <summary>Prueft ob eine Haltung im Benchmark-Set ist (fuer KB-Schutz).</summary>
    public async Task<bool> IsBenchmarkHaltungAsync(string haltungId, CancellationToken ct = default)
    {
        var set = await LoadAsync(ct).ConfigureAwait(false);
        return set.Haltungen.Exists(h =>
            string.Equals(h.HaltungId, haltungId, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Gesamtes Benchmark-Set.</summary>
public sealed class BenchmarkSet
{
    public List<BenchmarkHaltung> Haltungen { get; set; } = [];
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunUtc { get; set; }
}

/// <summary>Eine Benchmark-Haltung mit Goldstandard.</summary>
public sealed class BenchmarkHaltung
{
    public required string HaltungId { get; set; }
    public required string VideoPath { get; set; }
    public required string ProtocolSource { get; set; }
    public required string SourceType { get; set; }
    public string? Rohrmaterial { get; set; }
    public int? NennweiteMm { get; set; }
    public string? Kamerasystem { get; set; }

    /// <summary>Manuell verifizierter Goldstandard — unveraenderlich nach Erstellung.</summary>
    public List<GroundTruthEntry> GoldStandard { get; set; } = [];
}
