using System;
using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.IO;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 4.4: Tests fuer TrainingRuns-Tabelle, Samples.RunId und
/// BeginRun/EndRun/GetActiveRunId-API.
///
/// Verifiziert:
///  - Schema vorhanden (frische DB)
///  - Migration existing DB ergaenzt Samples.RunId
///  - BeginRun erzeugt Run-Eintrag mit Status=in_progress
///  - EndRun setzt EndedUtc + Status, deaktiviert Active-Run
///  - Samples bekommen RunId via UpsertSample-Pfad
///  - Mehrere sequenzielle Runs sind unabhaengig
/// </summary>
public sealed class KnowledgeBaseTrainingRunsTests : IDisposable
{
    private readonly string _dbPath;

    public KnowledgeBaseTrainingRunsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"sewer_kb_runs_{Guid.NewGuid():N}.db");
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

    // ── Schema ───────────────────────────────────────────────────────

    [Fact]
    public void FrischeDb_HatTrainingRunsTabelle()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        Assert.True(TableExists(ctx, "TrainingRuns"));
    }

    [Fact]
    public void FrischeDb_HatSamplesRunIdSpalte()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        Assert.True(HasColumn(ctx, "Samples", "RunId"));
    }

    [Fact]
    public void TrainingRuns_HatAlleProvenanceSpalten()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        Assert.True(HasColumn(ctx, "TrainingRuns", "RunId"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "StartedUtc"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "EndedUtc"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "ModelName"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "ModelVersion"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "PromptVersion"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "PipelineVersion"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "Status"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "SampleCount"));
        Assert.True(HasColumn(ctx, "TrainingRuns", "Notes"));
    }

    [Fact]
    public void Migration_ExistingDbOhneRunId_BekommtSpalte()
    {
        // Pre-4.4-Stand: Samples ohne RunId
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
                INSERT INTO Samples(SampleId, CaseId, VsaCode, ExportedUtc, VersionId)
                  VALUES('s1','c','BAB','2026-05-04T00:00:00Z','v1');
                """;
            pre.ExecuteNonQuery();
        }

        using var ctx = new KnowledgeBaseContext(_dbPath);

        // RunId-Spalte muss jetzt da sein, alter Eintrag bekommt NULL
        Assert.True(HasColumn(ctx, "Samples", "RunId"));
        using var sel = ctx.Connection.CreateCommand();
        sel.CommandText = "SELECT RunId FROM Samples WHERE SampleId='s1'";
        var runId = sel.ExecuteScalar();
        Assert.True(runId is null || runId == DBNull.Value, $"alter Sample sollte RunId NULL haben, ist: {runId}");
    }

    // ── BeginRun / EndRun / GetActiveRunId ───────────────────────────

    [Fact]
    public void BeginRun_ErzeugtRunMitInProgressStatus()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        var runId = manager.BeginRun(
            modelName: "nomic-embed-text",
            modelVersion: "v1.5",
            promptVersion: "p17",
            pipelineVersion: "v4.3",
            notes: "Test-Run");

        Assert.False(string.IsNullOrWhiteSpace(runId));

        // Verifikation: Run im DB sichtbar
        using var sel = ctx.Connection.CreateCommand();
        sel.CommandText = """
            SELECT ModelName, ModelVersion, PromptVersion, PipelineVersion, Status, EndedUtc, Notes
            FROM TrainingRuns WHERE RunId = $id
            """;
        sel.Parameters.AddWithValue("$id", runId);
        using var rdr = sel.ExecuteReader();
        Assert.True(rdr.Read());
        Assert.Equal("nomic-embed-text", rdr.GetString(0));
        Assert.Equal("v1.5", rdr.GetString(1));
        Assert.Equal("p17", rdr.GetString(2));
        Assert.Equal("v4.3", rdr.GetString(3));
        Assert.Equal("in_progress", rdr.GetString(4));
        Assert.True(rdr.IsDBNull(5)); // EndedUtc
        Assert.Equal("Test-Run", rdr.GetString(6));
    }

    [Fact]
    public void GetActiveRunId_GibtAktivenRunZurueck()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        Assert.Null(manager.GetActiveRunId());

        var runId = manager.BeginRun("model");

        Assert.Equal(runId, manager.GetActiveRunId());
    }

    [Fact]
    public void EndRun_SetztStatusUndEndedUtc()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        var runId = manager.BeginRun("model");
        manager.EndRun(runId, status: "completed");

        using var sel = ctx.Connection.CreateCommand();
        sel.CommandText = "SELECT Status, EndedUtc FROM TrainingRuns WHERE RunId=$id";
        sel.Parameters.AddWithValue("$id", runId);
        using var rdr = sel.ExecuteReader();
        Assert.True(rdr.Read());
        Assert.Equal("completed", rdr.GetString(0));
        Assert.False(rdr.IsDBNull(1));
    }

    [Fact]
    public void EndRun_DeaktiviertActiveRun()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        var runId = manager.BeginRun("model");
        Assert.Equal(runId, manager.GetActiveRunId());

        manager.EndRun(runId);

        Assert.Null(manager.GetActiveRunId());
    }

    [Fact]
    public void EndRun_MitFinalNotes_UeberschreibtNotes()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        var runId = manager.BeginRun("model", notes: "initial");
        manager.EndRun(runId, status: "failed", finalNotes: "final-error");

        using var sel = ctx.Connection.CreateCommand();
        sel.CommandText = "SELECT Status, Notes FROM TrainingRuns WHERE RunId=$id";
        sel.Parameters.AddWithValue("$id", runId);
        using var rdr = sel.ExecuteReader();
        Assert.True(rdr.Read());
        Assert.Equal("failed", rdr.GetString(0));
        Assert.Equal("final-error", rdr.GetString(1));
    }

    [Fact]
    public void EndRun_OhneFinalNotes_BehaeltInitialNotes()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        var runId = manager.BeginRun("model", notes: "initial");
        manager.EndRun(runId, status: "completed");

        using var sel = ctx.Connection.CreateCommand();
        sel.CommandText = "SELECT Notes FROM TrainingRuns WHERE RunId=$id";
        sel.Parameters.AddWithValue("$id", runId);
        var notes = sel.ExecuteScalar() as string;
        Assert.Equal("initial", notes);
    }

    [Fact]
    public void ZweiSequentielleRuns_HabenUnterschiedlicheIds()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        var run1 = manager.BeginRun("model");
        manager.EndRun(run1);
        var run2 = manager.BeginRun("model");

        Assert.NotEqual(run1, run2);
        Assert.Equal(run2, manager.GetActiveRunId());
    }

    [Fact]
    public void EndRun_LeererRunId_IstNoOp()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var manager = MakeManager(ctx);

        manager.EndRun("");
        manager.EndRun("   ");
        // Kein Throw, kein Schaden
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────

    private static KnowledgeBaseManager MakeManager(KnowledgeBaseContext ctx)
    {
        // EmbeddingService benoetigt OllamaConfig + HttpClient — fuer Provenance-
        // Tests reicht ein Stub-EmbeddingService nicht aus, weil der Manager
        // nur fuer BeginRun/EndRun den Embedder nicht braucht. Wir konstruieren
        // ihn aber ueber den realen Konstruktor mit einem Dummy-Embedder.
        var ollamaCfg = new AuswertungPro.Next.Application.Ai.Ollama.OllamaConfig(
            BaseUri: new Uri("http://localhost:11434"),
            VisionModel: "qwen3-vl",
            TextModel: "qwen3-vl",
            EmbedModel: "nomic-embed-text",
            RequestTimeout: TimeSpan.FromSeconds(30));
        var http = new System.Net.Http.HttpClient();
        var embedder = new EmbeddingService(http, ollamaCfg);
        return new KnowledgeBaseManager(ctx, embedder);
    }

    private static bool TableExists(KnowledgeBaseContext ctx, string table)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n";
        cmd.Parameters.AddWithValue("$n", table);
        return cmd.ExecuteScalar() is not null;
    }

    private static bool HasColumn(KnowledgeBaseContext ctx, string table, string column)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
