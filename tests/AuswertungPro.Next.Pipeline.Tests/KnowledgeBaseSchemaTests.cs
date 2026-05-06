using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 2.1: Schema-Tests fuer KnowledgeBaseContext.
/// Prueft FK-Constraint Embeddings -> Samples + ModelVersion-Spalte.
/// Sind reine Schema-/Migration-Tests, keine GPU-/Ollama-Abhaengigkeit.
/// </summary>
public sealed class KnowledgeBaseSchemaTests : IDisposable
{
    private readonly string _dbPath;

    public KnowledgeBaseSchemaTests()
    {
        // Eigene Temp-DB pro Testlauf (verhindert Test-Querkontamination)
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"sewer_kb_schema_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        // Aufraeumen — auch WAL- und SHM-Files
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    // ── FK-Constraint ────────────────────────────────────────────────

    [Fact]
    public void FrischeDb_HatForeignKeyAufSamples()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        var fkExists = HasForeignKey(ctx, "Embeddings", "Samples");

        Assert.True(fkExists,
            "Phase 2.1: Embeddings-Tabelle muss FK auf Samples haben.");
    }

    [Fact]
    public void OrphanEmbedding_OhneSample_WirftFkConstraint()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        // Versuche, ein Embedding ohne zugehoeriges Sample einzufuegen.
        var ex = Assert.Throws<SqliteException>(() =>
        {
            using var cmd = ctx.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
                VALUES ('orphan_id', 'nomic-embed-text', '', x'0102', '2026-05-04T00:00:00Z')
                """;
            cmd.ExecuteNonQuery();
        });

        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmbeddingMitGueltigemSample_WirdAkzeptiert()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        // 1) Sample anlegen
        InsertSample(ctx, "sample_001");

        // 2) Embedding mit gueltigem SampleId
        InsertEmbedding(ctx, "sample_001", model: "nomic-embed-text", modelVersion: "F16");

        // 3) Verifizieren
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId='sample_001'";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public void SampleDelete_LoeschtEmbeddingKaskadiert()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        InsertSample(ctx, "sample_002");
        InsertEmbedding(ctx, "sample_002", model: "nomic-embed-text", modelVersion: "");

        // Sample loeschen
        using (var del = ctx.Connection.CreateCommand())
        {
            del.CommandText = "DELETE FROM Samples WHERE SampleId='sample_002'";
            del.ExecuteNonQuery();
        }

        // Embedding muss durch CASCADE auch weg sein
        using var check = ctx.Connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId='sample_002'";
        var count = Convert.ToInt64(check.ExecuteScalar());
        Assert.Equal(0, count);
    }

    // ── ModelVersion-Spalte ──────────────────────────────────────────

    [Fact]
    public void EmbeddingsTabelle_HatModelVersionSpalte()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        var hasColumn = HasColumn(ctx, "Embeddings", "ModelVersion");

        Assert.True(hasColumn,
            "Phase 2.1: Embeddings.ModelVersion muss vorhanden sein.");
    }

    [Fact]
    public void ModelVersion_WirdGespeichertUndGelesen()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        InsertSample(ctx, "sample_003");
        InsertEmbedding(ctx, "sample_003",
            model: "nomic-embed-text", modelVersion: "v1.5-F16");

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "SELECT ModelVersion FROM Embeddings WHERE SampleId='sample_003'";
        var stored = cmd.ExecuteScalar() as string;

        Assert.Equal("v1.5-F16", stored);
    }

    [Fact]
    public void ModelVersion_LeerErlaubt_FuerAlteEmbeddings()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        InsertSample(ctx, "sample_004");
        InsertEmbedding(ctx, "sample_004",
            model: "nomic-embed-text", modelVersion: "");

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "SELECT ModelVersion FROM Embeddings WHERE SampleId='sample_004'";
        var stored = cmd.ExecuteScalar() as string;

        Assert.Equal("", stored);
    }

    // ── Migration: existierende DB ohne FK ───────────────────────────

    [Fact]
    public void Migration_ExistingDbOhneFk_WirdUmgebaut()
    {
        // 1) Pre-2.1-Stand simulieren: Embeddings-Tabelle ohne FK + ohne ModelVersion
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var pre = conn.CreateCommand();
            pre.CommandText = """
                CREATE TABLE Samples (
                    SampleId TEXT PRIMARY KEY,
                    CaseId TEXT NOT NULL,
                    VsaCode TEXT NOT NULL,
                    Beschreibung TEXT NOT NULL DEFAULT '',
                    MeterStart REAL NOT NULL DEFAULT 0,
                    MeterEnd REAL NOT NULL DEFAULT 0,
                    IsStreck INTEGER NOT NULL DEFAULT 0,
                    FramePath TEXT NOT NULL DEFAULT '',
                    ExportedUtc TEXT NOT NULL,
                    VersionId TEXT NOT NULL
                );
                CREATE TABLE Embeddings (
                    SampleId TEXT PRIMARY KEY,
                    Model TEXT NOT NULL,
                    Vector BLOB NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                INSERT INTO Samples(SampleId, CaseId, VsaCode, ExportedUtc, VersionId)
                  VALUES('valid_sample','case_a','BAB','2026-05-04T00:00:00Z','v1');
                INSERT INTO Embeddings(SampleId, Model, Vector, CreatedAt)
                  VALUES('valid_sample','nomic','data','2026-05-04T00:00:00Z'),
                        ('orphan_x','nomic','data','2026-05-04T00:00:00Z'),
                        ('orphan_y','nomic','data','2026-05-04T00:00:00Z');
                """;
            pre.ExecuteNonQuery();
        }

        // 2) Context oeffnet -> Migration laeuft
        using (var ctx = new KnowledgeBaseContext(_dbPath))
        {
            // FK existiert jetzt
            Assert.True(HasForeignKey(ctx, "Embeddings", "Samples"));

            // ModelVersion existiert jetzt
            Assert.True(HasColumn(ctx, "Embeddings", "ModelVersion"));

            // Saubere Embeddings sind ueberlebt
            using var cnt = ctx.Connection.CreateCommand();
            cnt.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId='valid_sample'";
            Assert.Equal(1L, Convert.ToInt64(cnt.ExecuteScalar()));

            // Orphans sind weg aus Embeddings
            using var orph = ctx.Connection.CreateCommand();
            orph.CommandText = "SELECT COUNT(*) FROM Embeddings WHERE SampleId LIKE 'orphan_%'";
            Assert.Equal(0L, Convert.ToInt64(orph.ExecuteScalar()));

            // Aber sie sind in Embeddings_orphan archiviert (kein Datenverlust)
            using var arch = ctx.Connection.CreateCommand();
            arch.CommandText = "SELECT COUNT(*) FROM Embeddings_orphan";
            Assert.Equal(2L, Convert.ToInt64(arch.ExecuteScalar()));
        }
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────

    private static void InsertSample(KnowledgeBaseContext ctx, string sampleId)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Samples(SampleId, CaseId, VsaCode, ExportedUtc, VersionId)
            VALUES($id, 'case_test', 'BAB', '2026-05-04T00:00:00Z', 'v_test')
            """;
        cmd.Parameters.AddWithValue("$id", sampleId);
        cmd.ExecuteNonQuery();
    }

    private static void InsertEmbedding(
        KnowledgeBaseContext ctx, string sampleId, string model, string modelVersion)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Embeddings(SampleId, Model, ModelVersion, Vector, CreatedAt)
            VALUES($id, $model, $mv, x'0102', '2026-05-04T00:00:00Z')
            """;
        cmd.Parameters.AddWithValue("$id", sampleId);
        cmd.Parameters.AddWithValue("$model", model);
        cmd.Parameters.AddWithValue("$mv", modelVersion);
        cmd.ExecuteNonQuery();
    }

    private static bool HasForeignKey(KnowledgeBaseContext ctx, string table, string referenced)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"PRAGMA foreign_key_list({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var refTable = reader.IsDBNull(2) ? null : reader.GetString(2);
            if (string.Equals(refTable, referenced, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool HasColumn(KnowledgeBaseContext ctx, string table, string column)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // PRAGMA table_info: cid, name, type, notnull, dflt_value, pk
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
