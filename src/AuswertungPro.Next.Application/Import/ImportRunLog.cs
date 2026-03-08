using System.Collections.Concurrent;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public IReadOnlyCollection<ImportRunLogEntry> Entries => _entries;

    public List<ImportRunLogEntry> EntriesList => _entries.OrderBy(e => e.TimestampUtc).ToList();

    // Summary counters
    public int TotalCreated => _entries.Count(e => e.Status == ImportLogStatus.Created);
    public int TotalUpdated => _entries.Count(e => e.Status == ImportLogStatus.Updated);
    public int TotalSkipped => _entries.Count(e => e.Status == ImportLogStatus.Skipped);
    public int TotalConflicts => _entries.Count(e => e.Status == ImportLogStatus.Conflict);
    public int TotalErrors => _entries.Count(e => e.Status == ImportLogStatus.Error);

    public TimeSpan Duration => (CompletedAtUtc ?? DateTime.UtcNow) - StartedAtUtc;

    public void AddEntry(ImportRunLogEntry entry) => _entries.Add(entry);

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
    }

    public void Complete()
    {
        CompletedAtUtc = DateTime.UtcNow;
    }
}
