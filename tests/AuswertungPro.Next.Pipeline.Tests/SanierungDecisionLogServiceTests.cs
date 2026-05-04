using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Sanierung;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Sanierung;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 5.5: Tests fuer SanierungDecisionLogService — persistiert
/// RulesEvaluation-Ergebnisse mit Provenance (Knowledge-Version, RunId).
/// </summary>
public sealed class SanierungDecisionLogServiceTests : IDisposable
{
    private readonly string _dbPath;

    public SanierungDecisionLogServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"sewer_decision_log_{Guid.NewGuid():N}.db");
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
    public void FrischeDb_HatSanierungDecisionLogTabelle()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SanierungDecisionLog'";
        Assert.NotNull(cmd.ExecuteScalar());
    }

    // ── LogEvaluation ────────────────────────────────────────────────

    [Fact]
    public void LogEvaluation_PersistiertVollstaendigenEintrag()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);

        var engine = new RehabilitationRulesEngine();
        var ctxIn = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB", "BAJ" };
        var eval = engine.Evaluate(ctxIn, codes);

        var decisionId = service.LogEvaluation(
            context: "AWU.Hauptstrasse.H42",
            vsaCodes: codes,
            eval: eval,
            knowledgeVersion: "1.1",
            runId: "abc123",
            notes: "Erstgutachten");

        Assert.False(string.IsNullOrWhiteSpace(decisionId));

        var entry = service.GetById(decisionId);
        Assert.NotNull(entry);
        Assert.Equal("AWU.Hauptstrasse.H42", entry!.Context);
        Assert.Equal("1.1", entry.KnowledgeVersion);
        Assert.Equal("abc123", entry.RunId);
        Assert.Equal("Erstgutachten", entry.Notes);
        Assert.False(string.IsNullOrWhiteSpace(entry.CreatedUtc));
    }

    [Fact]
    public void LogEvaluation_VsaCodesJsonIstParsbar()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);
        var engine = new RehabilitationRulesEngine();
        var ctxIn = new HaltungsKontext { DnMm = 300 };
        var codes = new[] { "BAB", "BCA" };
        var eval = engine.Evaluate(ctxIn, codes);

        var id = service.LogEvaluation("c", codes, eval);
        var entry = service.GetById(id);

        var parsed = JsonSerializer.Deserialize<string[]>(entry!.VsaCodesJson);
        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Length);
        Assert.Contains("BAB", parsed);
        Assert.Contains("BCA", parsed);
    }

    [Fact]
    public void LogEvaluation_EligibleProceduresEnthaltenIdNameReason()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);
        var engine = new RehabilitationRulesEngine();
        var ctxIn = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB" }; // cracks
        var eval = engine.Evaluate(ctxIn, codes);

        var id = service.LogEvaluation("c", codes, eval);
        var entry = service.GetById(id);

        // EligibleJson sollte cipp_inliner enthalten (cracks -> eligible bei Beton DN300)
        Assert.Contains("cipp_inliner", entry!.EligibleJson);
        Assert.Contains("\"reason\":", entry.EligibleJson);
    }

    [Fact]
    public void LogEvaluation_OhneRunId_PersistiertNull()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);
        var engine = new RehabilitationRulesEngine();
        var eval = engine.Evaluate(new HaltungsKontext { DnMm = 300 }, new[] { "BAB" });

        var id = service.LogEvaluation("c", new[] { "BAB" }, eval, runId: null);
        var entry = service.GetById(id);

        Assert.Null(entry!.RunId);
    }

    // ── Lese-API ─────────────────────────────────────────────────────

    [Fact]
    public void GetRecent_LiefertNeuesteZuerst()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);
        var engine = new RehabilitationRulesEngine();
        var eval = engine.Evaluate(new HaltungsKontext { DnMm = 300 }, new[] { "BAB" });

        var id1 = service.LogEvaluation("first", new[] { "BAB" }, eval);
        System.Threading.Thread.Sleep(15); // damit CreatedUtc unterschiedlich ist
        var id2 = service.LogEvaluation("second", new[] { "BAB" }, eval);
        System.Threading.Thread.Sleep(15);
        var id3 = service.LogEvaluation("third", new[] { "BAB" }, eval);

        var recent = service.GetRecent(limit: 10);
        Assert.True(recent.Count >= 3);
        Assert.Equal(id3, recent[0].DecisionId);
        Assert.Equal(id2, recent[1].DecisionId);
        Assert.Equal(id1, recent[2].DecisionId);
    }

    [Fact]
    public void GetByContext_FiltertKorrekt()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);
        var engine = new RehabilitationRulesEngine();
        var eval = engine.Evaluate(new HaltungsKontext { DnMm = 300 }, new[] { "BAB" });

        service.LogEvaluation("AWU.A", new[] { "BAB" }, eval);
        service.LogEvaluation("AWU.B", new[] { "BAB" }, eval);
        service.LogEvaluation("AWU.A", new[] { "BAB" }, eval);

        var result = service.GetByContext("AWU.A");
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("AWU.A", e.Context));
    }

    [Fact]
    public void GetById_NichtExistierendeId_GibtNullZurueck()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);

        var entry = service.GetById("nonexistent_id");

        Assert.Null(entry);
    }

    [Fact]
    public void LogEvaluation_NullEval_Wirft()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var service = new SanierungDecisionLogService(ctx);

        Assert.Throws<ArgumentNullException>(() =>
            service.LogEvaluation("c", new[] { "BAB" }, eval: null!));
    }
}
