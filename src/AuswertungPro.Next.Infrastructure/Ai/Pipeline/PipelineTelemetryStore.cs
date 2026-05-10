using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Common;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Sprint 2 (2026-05-07): Pipeline-Telemetry-Persistierung in SQLite.
///
/// Hintergrund: Bisher schreibt <c>PipelineTelemetry.PersistSummaryAsync</c>
/// nur eine JSONL-Zeile pro Lauf in <c>logs/pipeline_telemetry.jsonl</c>.
/// Das ist gut fuer schnelle Inspektion, aber unbrauchbar fuer Trends ueber
/// Wochen (Drift-Erkennung Latenz, Frame-Counts, Throughput).
///
/// Loesung: Separate SQLite-DB <c>%LOCALAPPDATA%/SewerStudio/pipeline_telemetry.db</c>
/// mit einer Tabelle <c>PipelineRuns</c>. Index auf <c>TimestampUtc</c> fuer
/// schnelle Trend-Queries. Die JSONL-Datei bleibt parallel als Backup
/// (Audit-Lesbarkeit, wenn die DB lockt oder fehlt).
///
/// Schema:
/// <code>
/// CREATE TABLE PipelineRuns (
///   Id INTEGER PRIMARY KEY AUTOINCREMENT,
///   TimestampUtc TEXT NOT NULL,
///   Label TEXT NOT NULL,
///   TotalFrames INTEGER NOT NULL,
///   SkippedFrames INTEGER NOT NULL,
///   WallClockMs INTEGER NOT NULL,
///   {Phase}MeanMs REAL NOT NULL,
///   {Phase}P95Ms REAL NOT NULL  -- pro Phase (Extraction/Yolo/Dino/Sam/Qwen/Total)
/// );
/// CREATE INDEX idx_pipelineruns_ts ON PipelineRuns(TimestampUtc);
/// </code>
/// </summary>
public sealed class PipelineTelemetryStore : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _conn;
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Process-weiter Default-Store. Lazy initialisiert beim ersten Zugriff.
    /// Wird von <see cref="PipelineTelemetryWiring"/> als
    /// <c>PipelineTelemetry.AdditionalPersister</c> eingehaengt, damit jeder
    /// Pipeline-Lauf automatisch in SQLite landet.
    /// </summary>
    private static readonly Lazy<PipelineTelemetryStore> _default = new(() => new PipelineTelemetryStore());
    public static PipelineTelemetryStore Default => _default.Value;

    public PipelineTelemetryStore(string? customDbPath = null)
    {
        _dbPath = customDbPath ?? PathConstants.InAppData(PathConstants.PipelineTelemetryDb);
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate");
        _conn.Open();

        // SQLite-Pragmas: WAL fuer parallele Reader, busy_timeout damit der Writer wartet
        using var pragma = _conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();

        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS PipelineRuns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampUtc TEXT NOT NULL,
                Label TEXT NOT NULL,
                TotalFrames INTEGER NOT NULL,
                SkippedFrames INTEGER NOT NULL,
                WallClockMs INTEGER NOT NULL,
                ExtractionMeanMs REAL NOT NULL,
                ExtractionP95Ms REAL NOT NULL,
                YoloMeanMs REAL NOT NULL,
                YoloP95Ms REAL NOT NULL,
                DinoMeanMs REAL NOT NULL,
                DinoP95Ms REAL NOT NULL,
                SamMeanMs REAL NOT NULL,
                SamP95Ms REAL NOT NULL,
                QwenMeanMs REAL NOT NULL,
                QwenP95Ms REAL NOT NULL,
                TotalMeanMs REAL NOT NULL,
                TotalP95Ms REAL NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_pipelineruns_ts ON PipelineRuns(TimestampUtc);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Schreibt eine Telemetry-Zusammenfassung. Thread-safe via SemaphoreSlim.</summary>
    public async Task SaveRunAsync(string label, TelemetrySummary summary, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO PipelineRuns (
                    TimestampUtc, Label, TotalFrames, SkippedFrames, WallClockMs,
                    ExtractionMeanMs, ExtractionP95Ms,
                    YoloMeanMs, YoloP95Ms,
                    DinoMeanMs, DinoP95Ms,
                    SamMeanMs, SamP95Ms,
                    QwenMeanMs, QwenP95Ms,
                    TotalMeanMs, TotalP95Ms)
                VALUES (
                    $ts, $label, $total, $skipped, $wall,
                    $exMean, $exP95,
                    $yMean, $yP95,
                    $dMean, $dP95,
                    $sMean, $sP95,
                    $qMean, $qP95,
                    $tMean, $tP95);
                """;
            cmd.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$label", label ?? string.Empty);
            cmd.Parameters.AddWithValue("$total", summary.TotalFrames);
            cmd.Parameters.AddWithValue("$skipped", summary.SkippedFrames);
            cmd.Parameters.AddWithValue("$wall", summary.WallClockMs);
            cmd.Parameters.AddWithValue("$exMean", summary.Extraction.MeanMs);
            cmd.Parameters.AddWithValue("$exP95", summary.Extraction.P95Ms);
            cmd.Parameters.AddWithValue("$yMean", summary.Yolo.MeanMs);
            cmd.Parameters.AddWithValue("$yP95", summary.Yolo.P95Ms);
            cmd.Parameters.AddWithValue("$dMean", summary.Dino.MeanMs);
            cmd.Parameters.AddWithValue("$dP95", summary.Dino.P95Ms);
            cmd.Parameters.AddWithValue("$sMean", summary.Sam.MeanMs);
            cmd.Parameters.AddWithValue("$sP95", summary.Sam.P95Ms);
            cmd.Parameters.AddWithValue("$qMean", summary.Qwen.MeanMs);
            cmd.Parameters.AddWithValue("$qP95", summary.Qwen.P95Ms);
            cmd.Parameters.AddWithValue("$tMean", summary.Total.MeanMs);
            cmd.Parameters.AddWithValue("$tP95", summary.Total.P95Ms);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Liest die letzten N Laeufe, juengste zuerst.</summary>
    public async Task<IReadOnlyList<TelemetryRunSnapshot>> GetRecentRunsAsync(int limit = 100, CancellationToken ct = default)
    {
        var results = new List<TelemetryRunSnapshot>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, TimestampUtc, Label, TotalFrames, SkippedFrames, WallClockMs,
                   ExtractionMeanMs, ExtractionP95Ms,
                   YoloMeanMs, YoloP95Ms,
                   DinoMeanMs, DinoP95Ms,
                   SamMeanMs, SamP95Ms,
                   QwenMeanMs, QwenP95Ms,
                   TotalMeanMs, TotalP95Ms
            FROM PipelineRuns
            ORDER BY Id DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(ReadSnapshot(reader));
        }
        return results;
    }

    /// <summary>Liest alle Laeufe seit einem bestimmten Zeitpunkt (UTC).</summary>
    public async Task<IReadOnlyList<TelemetryRunSnapshot>> GetRunsSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
    {
        var results = new List<TelemetryRunSnapshot>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, TimestampUtc, Label, TotalFrames, SkippedFrames, WallClockMs,
                   ExtractionMeanMs, ExtractionP95Ms,
                   YoloMeanMs, YoloP95Ms,
                   DinoMeanMs, DinoP95Ms,
                   SamMeanMs, SamP95Ms,
                   QwenMeanMs, QwenP95Ms,
                   TotalMeanMs, TotalP95Ms
            FROM PipelineRuns
            WHERE TimestampUtc >= $since
            ORDER BY TimestampUtc ASC;
            """;
        cmd.Parameters.AddWithValue("$since", sinceUtc.ToString("O", CultureInfo.InvariantCulture));
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(ReadSnapshot(reader));
        }
        return results;
    }

    /// <summary>Anzahl persistierter Laeufe — fuer Diagnose/UI.</summary>
    public async Task<long> GetRunCountAsync(CancellationToken ct = default)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PipelineRuns;";
        var raw = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return raw is long l ? l : Convert.ToInt64(raw, CultureInfo.InvariantCulture);
    }

    private static TelemetryRunSnapshot ReadSnapshot(SqliteDataReader r)
    {
        return new TelemetryRunSnapshot(
            Id: r.GetInt64(0),
            TimestampUtc: DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Label: r.GetString(2),
            TotalFrames: r.GetInt32(3),
            SkippedFrames: r.GetInt32(4),
            WallClockMs: r.GetInt64(5),
            ExtractionMeanMs: r.GetDouble(6),
            ExtractionP95Ms: r.GetDouble(7),
            YoloMeanMs: r.GetDouble(8),
            YoloP95Ms: r.GetDouble(9),
            DinoMeanMs: r.GetDouble(10),
            DinoP95Ms: r.GetDouble(11),
            SamMeanMs: r.GetDouble(12),
            SamP95Ms: r.GetDouble(13),
            QwenMeanMs: r.GetDouble(14),
            QwenP95Ms: r.GetDouble(15),
            TotalMeanMs: r.GetDouble(16),
            TotalP95Ms: r.GetDouble(17));
    }

    public void Dispose()
    {
        // Best-effort Cleanup: SQLite-Connection kann beim Shutdown schon
        // disposed sein, das ist kein Loggable Failure.
        try { _conn.Close(); _conn.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PipelineTelemetryStore] Dispose: {ex.Message}"); }
    }

    /// <summary>
    /// Sprint 2 (2026-05-07): Verdrahtet die SQLite-Persistierung an eine
    /// frische <see cref="PipelineTelemetry"/>-Instanz. Aufrufer ruft das
    /// genau einmal pro Telemetry-Objekt direkt nach dem Konstruktor:
    /// <code>
    /// var telemetry = new PipelineTelemetry();
    /// PipelineTelemetryStore.EnableSqlitePersistence(telemetry);
    /// </code>
    /// Damit landet jeder spaetere <c>PersistSummaryAsync</c>-Aufruf
    /// zusaetzlich in <see cref="Default"/>. Failures im Hook werden
    /// in <c>PipelineTelemetry</c> selbst geschluckt — JSONL bleibt der
    /// zuverlaessige Pfad.
    /// </summary>
    public static void EnableSqlitePersistence(PipelineTelemetry telemetry)
    {
        if (telemetry is null) throw new ArgumentNullException(nameof(telemetry));
        telemetry.AdditionalPersister = (label, summary, ct) =>
            Default.SaveRunAsync(label, summary, ct);
    }
}
