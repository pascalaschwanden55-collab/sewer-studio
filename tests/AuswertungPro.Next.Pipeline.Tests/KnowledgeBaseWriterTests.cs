using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 2.2: Tests fuer KnowledgeBaseWriter + zentrale PRAGMAs.
/// Verifiziert:
///  - PRAGMAs sind aktiv (busy_timeout, foreign_keys, journal_mode, synchronous)
///  - Writer serialisiert parallele Writes
///  - FK bleibt nach Phase 2.2 aktiv
///  - Kein "database is locked" bei kontrollierter Parallelitaet
/// </summary>
[Trait("Category", "Integration")]
public sealed class KnowledgeBaseWriterTests : IDisposable
{
    private readonly string _dbPath;

    public KnowledgeBaseWriterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"sewer_kb_writer_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    // ── PRAGMAs ──────────────────────────────────────────────────────

    [Fact]
    public void Pragma_ForeignKeys_IstAktiv()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys;";
        var result = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Equal(1L, result); // 1 = ON
    }

    [Fact]
    public void Pragma_BusyTimeout_IstGesetzt()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout;";
        var result = Convert.ToInt64(cmd.ExecuteScalar());

        Assert.Equal(5000L, result);
    }

    [Fact]
    public void Pragma_JournalMode_IstWal()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var result = cmd.ExecuteScalar() as string;

        Assert.Equal("wal", result, ignoreCase: true);
    }

    [Fact]
    public void Pragma_Synchronous_IstNormal()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "PRAGMA synchronous;";
        var result = Convert.ToInt64(cmd.ExecuteScalar());

        // PRAGMA synchronous: 0=OFF, 1=NORMAL, 2=FULL, 3=EXTRA
        Assert.Equal(1L, result);
    }

    // ── Writer-Lock ──────────────────────────────────────────────────

    [Fact]
    public void Writer_Execute_LaeuftSequentiell()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var writer = new KnowledgeBaseWriter(ctx);

        var counter = 0;
        var maxConcurrent = 0;
        var current = 0;
        var lockObj = new object();

        // 20 parallele Tasks, die ein Increment + kleine Arbeit machen.
        // Wenn der Writer NICHT serialisiert, koennen mehrere gleichzeitig drin sein.
        Parallel.For(0, 20, _ =>
        {
            writer.Execute(_ =>
            {
                lock (lockObj)
                {
                    current++;
                    if (current > maxConcurrent) maxConcurrent = current;
                }
                System.Threading.Thread.Sleep(5);
                lock (lockObj) { current--; }
                System.Threading.Interlocked.Increment(ref counter);
            });
        });

        Assert.Equal(20, counter);
        Assert.Equal(1, maxConcurrent); // Genau 1 Schreiber gleichzeitig
    }

    [Fact]
    public async Task Writer_ExecuteAsync_LaeuftSequentiell()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var writer = new KnowledgeBaseWriter(ctx);

        var current = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var tasks = new Task[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = writer.ExecuteAsync(async _ =>
            {
                lock (lockObj)
                {
                    current++;
                    if (current > maxConcurrent) maxConcurrent = current;
                }
                await Task.Delay(5);
                lock (lockObj) { current--; }
            });
        }
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public void Writer_ExecuteInTransaction_CommittetBeiErfolg()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var writer = new KnowledgeBaseWriter(ctx);

        InsertSample(ctx, "tx_ok_001");

        writer.ExecuteInTransaction((conn, tx) =>
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
                VALUES('tx_ok_001', 'm', '', x'0102', '2026-05-04T00:00:00Z')
                """;
            cmd.ExecuteNonQuery();
        });

        // Verifizieren
        using var check = ctx.Connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId='tx_ok_001'";
        Assert.Equal(1L, Convert.ToInt64(check.ExecuteScalar()));
    }

    [Fact]
    public void Writer_ExecuteInTransaction_RolltZurueckBeiException()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var writer = new KnowledgeBaseWriter(ctx);

        InsertSample(ctx, "tx_rb_001");

        Assert.Throws<InvalidOperationException>(() =>
        {
            writer.ExecuteInTransaction((conn, tx) =>
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
                    VALUES('tx_rb_001', 'm', '', x'0102', '2026-05-04T00:00:00Z')
                    """;
                cmd.ExecuteNonQuery();

                // Simulierter Fehler -> Rollback erwartet
                throw new InvalidOperationException("Test-Rollback");
            });
        });

        // Embedding darf NICHT existieren (Rollback erfolgreich)
        using var check = ctx.Connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId='tx_rb_001'";
        Assert.Equal(0L, Convert.ToInt64(check.ExecuteScalar()));
    }

    // ── Parallel-Stress ──────────────────────────────────────────────

    [Fact]
    public void Writer_Stress_KeinDatabaseIsLocked_BeiParallelenInserts()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var writer = new KnowledgeBaseWriter(ctx);

        // Pre-Inserts: Samples bereitstellen
        for (var i = 0; i < 50; i++)
            InsertSample(ctx, $"stress_{i:D3}");

        var errors = new ConcurrentBag<Exception>();

        Parallel.For(0, 50, i =>
        {
            try
            {
                writer.ExecuteInTransaction((conn, tx) =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = $"""
                        INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
                        VALUES('stress_{i:D3}', 'm', '', x'0102', '2026-05-04T00:00:00Z')
                        """;
                    cmd.ExecuteNonQuery();
                });
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        Assert.Empty(errors);

        using var cnt = ctx.Connection.CreateCommand();
        cnt.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId LIKE 'stress_%'";
        Assert.Equal(50L, Convert.ToInt64(cnt.ExecuteScalar()));
    }

    // ── FK-Constraint nach Phase 2.2 ─────────────────────────────────

    [Fact]
    public void ForeignKeyConstraint_BleibtAktivNachPhase22()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var writer = new KnowledgeBaseWriter(ctx);

        // Versuch: Embedding ohne Sample via Writer
        Assert.Throws<SqliteException>(() =>
        {
            writer.ExecuteInTransaction((conn, tx) =>
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
                    VALUES('orphan_via_writer', 'm', '', x'0102', '2026-05-04T00:00:00Z')
                    """;
                cmd.ExecuteNonQuery();
            });
        });
    }

    // ── Hilfsmethode ────────────────────────────────────────────────

    private static void InsertSample(KnowledgeBaseContext ctx, string sampleId)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Samples(SampleId, CaseId, VsaCode, ExportedUtc, VersionId)
            VALUES($id, 'c', 'BAB', '2026-05-04T00:00:00Z', 'v')
            """;
        cmd.Parameters.AddWithValue("$id", sampleId);
        cmd.ExecuteNonQuery();
    }
}
