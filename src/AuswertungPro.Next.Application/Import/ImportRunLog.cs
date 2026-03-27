using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading;

namespace AuswertungPro.Next.Application.Import;

public sealed class ImportRunLogEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Phase { get; init; } = "";
    public string Operation { get; init; } = "";
    public string? SourceFile { get; init; }
    public string? TargetPath { get; init; }
    public string? RecordKey { get; init; }
    public string? Field { get; init; }
    public ImportLogStatus Status { get; init; }
    public string? Detail { get; init; }
}

public enum ImportLogStatus
{
    Created,
    Updated,
    Skipped,
    Conflict,
    Error,
    Info
}

/// <summary>
/// Sammelt alle strukturierten Eintraege eines Import-Laufs.
/// Thread-safe: Entries koennen aus verschiedenen Threads geschrieben werden.
/// Counter via Interlocked (O(1) statt O(n) LINQ-Scan auf ConcurrentBag).
/// </summary>
public sealed class ImportRunLog
{
    public string RunId { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }
    public string ImportType { get; set; } = "";
    public string? SourcePath { get; set; }
    public bool WasDryRun { get; set; }
    public bool WasCancelled { get; set; }

    private readonly ConcurrentBag<ImportRunLogEntry> _entries = new();

    // Gecachte Counter (Thread-safe via Interlocked)
    private int _created, _updated, _skipped, _conflicts, _errors;

    [JsonIgnore]
    public IReadOnlyCollection<ImportRunLogEntry> Entries => _entries;

    public List<ImportRunLogEntry> EntriesList => _entries.OrderBy(e => e.TimestampUtc).ToList();

    // Summary counters (O(1) statt O(n))
    public int TotalCreated => _created;
    public int TotalUpdated => _updated;
    public int TotalSkipped => _skipped;
    public int TotalConflicts => _conflicts;
    public int TotalErrors => _errors;

    public TimeSpan Duration => (CompletedAtUtc ?? DateTime.UtcNow) - StartedAtUtc;

    public void AddEntry(ImportRunLogEntry entry)
    {
        _entries.Add(entry);
        IncrementCounter(entry.Status);
    }

    public void AddEntry(
        string phase,
        string operation,
        ImportLogStatus status,
        string? recordKey = null,
        string? field = null,
        string? detail = null,
        string? sourceFile = null,
        string? targetPath = null)
    {
        _entries.Add(new ImportRunLogEntry
        {
            Phase = phase,
            Operation = operation,
            Status = status,
            RecordKey = recordKey,
            Field = field,
            Detail = detail,
            SourceFile = sourceFile,
            TargetPath = targetPath
        });
        IncrementCounter(status);
    }

    private void IncrementCounter(ImportLogStatus status)
    {
        switch (status)
        {
            case ImportLogStatus.Created:  Interlocked.Increment(ref _created); break;
            case ImportLogStatus.Updated:  Interlocked.Increment(ref _updated); break;
            case ImportLogStatus.Skipped:  Interlocked.Increment(ref _skipped); break;
            case ImportLogStatus.Conflict: Interlocked.Increment(ref _conflicts); break;
            case ImportLogStatus.Error:    Interlocked.Increment(ref _errors); break;
        }
    }

    public void Complete()
    {
        CompletedAtUtc = DateTime.UtcNow;
    }
}
