using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.SelfImproving;

/// <summary>
/// Schreibt taegliche <see cref="KbDashboardSnapshot"/>-Eintraege als JSONL,
/// damit Trend und Drift ueber Wochen sichtbar werden. Jede Zeile enthaelt
/// den vollen Snapshot inklusive Zeitstempel (UTC).
///
/// Phase 2.4 des Audit-Konsens: ohne diese Persistierung sind
/// Accuracy- und QualityGate-Trends nur als Live-Ansicht im Dashboard
/// sichtbar - aber nicht reproduzierbar nach KB-Reindex oder App-Restart.
/// </summary>
public sealed class KbSnapshotJournal
{
    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public KbSnapshotJournal(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Haengt einen Eintrag an die JSONL-Datei an. Schreibt den Snapshot
    /// 1:1 als JSON pro Zeile - das laesst sich von jedem JSONL-Reader
    /// (Pandas, jq, Notebooks, Excel) konsumieren.
    /// </summary>
    /// <param name="snapshot">Aktueller Dashboard-Snapshot.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>true wenn geschrieben, false wenn doppelter Eintrag des gleichen Tages erkannt wurde.</returns>
    public async Task<bool> AppendAsync(KbDashboardSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Verzeichnis sicherstellen.
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Tagesgleichheits-Filter: wenn die letzte Zeile des Tages bereits
            // existiert, ueberspringen. Eine Zeile pro Tag reicht fuer Trends
            // und vermeidet 1000-fachen Schreiben bei einem Polling-UI.
            if (await IsAlreadyWrittenTodayAsync(snapshot.GeneratedUtc, ct).ConfigureAwait(false))
                return false;

            var json = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
            await File.AppendAllTextAsync(_path, json + Environment.NewLine, ct).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<bool> IsAlreadyWrittenTodayAsync(DateTime nowUtc, CancellationToken ct)
    {
        if (!File.Exists(_path)) return false;

        // Letzte Zeile lesen, ohne das ganze File einzuziehen.
        try
        {
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0) return false;
            var bufSize = (int)Math.Min(fs.Length, 4096);
            fs.Seek(-bufSize, SeekOrigin.End);
            var buffer = new byte[bufSize];
            var read = await fs.ReadAsync(buffer.AsMemory(0, bufSize), ct).ConfigureAwait(false);
            var tail = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            var lastNewline = tail.LastIndexOf('\n', tail.Length - 2);
            var lastLine = lastNewline >= 0 ? tail[(lastNewline + 1)..].Trim() : tail.Trim();
            if (string.IsNullOrEmpty(lastLine)) return false;

            using var doc = JsonDocument.Parse(lastLine);
            if (!doc.RootElement.TryGetProperty("GeneratedUtc", out var stamp)) return false;
            if (!stamp.TryGetDateTime(out var prev)) return false;
            return prev.Date == nowUtc.Date;
        }
        catch
        {
            // Wenn die letzte Zeile nicht parsbar ist, lieber ueberschreiben
            // als Datum verlieren - return false = wird angehaengt.
            return false;
        }
    }

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,                       // Properties bleiben PascalCase
    };

    /// <summary>
    /// Default-Pfad: %LOCALAPPDATA%/SewerStudio/logs/kb_snapshots.jsonl.
    /// Kann ueber den Konstruktor ueberschrieben werden (z.B. fuer Tests
    /// oder fuer Mirror-Speicherorte).
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", "logs", "kb_snapshots.jsonl");
}
