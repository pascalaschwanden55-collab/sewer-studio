using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer KnowledgeBaseDiagnosticsService-Erweiterungen (Quality-Verteilung,
/// ValidationLog-Aggregation) und den darauf aufbauenden KbDashboardService.
/// Reine SQLite-Tests, keine GPU-/Ollama-Abhaengigkeit.
/// </summary>
[Trait("Category", "Integration")]
public sealed class KbDashboardServiceTests : IDisposable
{
    private readonly string _dbPath;

    public KbDashboardServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"sewer_kb_dashboard_{Guid.NewGuid():N}.db");
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

    // ── ReadQualityDistribution ─────────────────────────────────────

    [Fact]
    public void QualityDistribution_LeereDb_LiefertNullen()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var diag = new KnowledgeBaseDiagnosticsService(ctx);

        var q = diag.ReadQualityDistribution();

        Assert.Equal(0, q.Green);
        Assert.Equal(0, q.Yellow);
        Assert.Equal(0, q.Red);
        Assert.Equal(0, q.Unknown);
    }

    [Fact]
    public void QualityDistribution_ZaehltGreenYellowRed_CaseInsensitive()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        InsertSample(ctx, "s1", code: "BAB", quality: "Green");
        InsertSample(ctx, "s2", code: "BAB", quality: "GREEN");
        InsertSample(ctx, "s3", code: "BAC", quality: "yellow");
        InsertSample(ctx, "s4", code: "BAC", quality: "Red");
        InsertSample(ctx, "s5", code: "BAC", quality: null); // -> Unknown
        InsertSample(ctx, "s6", code: "BAC", quality: "");   // -> Unknown

        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var q = diag.ReadQualityDistribution();

        Assert.Equal(2, q.Green);
        Assert.Equal(1, q.Yellow);
        Assert.Equal(1, q.Red);
        Assert.Equal(2, q.Unknown);
    }

    // ── ReadValidationStats ─────────────────────────────────────────

    [Fact]
    public void ValidationStats_LeerLog_LiefertLeereListe()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var stats = diag.ReadValidationStats();
        Assert.Empty(stats);
    }

    [Fact]
    public void ValidationStats_AggregiertProCode()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        // BAB: 3 von 4 korrekt (75%)
        InsertValidation(ctx, "BAB", correct: true);
        InsertValidation(ctx, "BAB", correct: true);
        InsertValidation(ctx, "BAB", correct: true);
        InsertValidation(ctx, "BAB", correct: false);
        // BAC: 1 von 5 korrekt (20%)
        InsertValidation(ctx, "BAC", correct: true);
        InsertValidation(ctx, "BAC", correct: false);
        InsertValidation(ctx, "BAC", correct: false);
        InsertValidation(ctx, "BAC", correct: false);
        InsertValidation(ctx, "BAC", correct: false);

        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var stats = diag.ReadValidationStats();

        Assert.Equal(2, stats.Count);
        var bab = stats.Find(s => s.VsaCode == "BAB")!;
        Assert.Equal(4, bab.Total);
        Assert.Equal(3, bab.Correct);
        var bac = stats.Find(s => s.VsaCode == "BAC")!;
        Assert.Equal(5, bac.Total);
        Assert.Equal(1, bac.Correct);
    }

    [Fact]
    public void OverallValidation_LiefertGesamtsumme()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        InsertValidation(ctx, "BAB", correct: true);
        InsertValidation(ctx, "BAC", correct: false);
        InsertValidation(ctx, "BAC", correct: true);

        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var (total, correct) = diag.ReadOverallValidation();

        Assert.Equal(3, total);
        Assert.Equal(2, correct);
    }

    // ── ProblemScore-Heuristik ──────────────────────────────────────

    [Fact]
    public void TopConfusions_AggregiertKorrekturen()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        InsertValidation(ctx, "BAC", correct: false, suggestedCode: "BAB", finalCode: "BAC");
        InsertValidation(ctx, "BAC", correct: false, suggestedCode: "BAB", finalCode: "BAC");
        InsertValidation(ctx, "BBA", correct: false, suggestedCode: "BBA", finalCode: "BAA");
        InsertValidation(ctx, "BAB", correct: true, suggestedCode: "BAB", finalCode: "BAB");

        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var dashboard = new KbDashboardService(diag) { TopConfusionsLimit = 5 };

        var direct = diag.ReadTopConfusions();
        var snap = dashboard.BuildSnapshot();

        Assert.Equal("BAB", direct[0].SuggestedCode);
        Assert.Equal("BAC", direct[0].FinalCode);
        Assert.Equal(2, direct[0].Count);

        Assert.Contains(snap.TopConfusions,
            c => c.SuggestedCode == "BAB" && c.FinalCode == "BAC" && c.Count == 2);
    }

    [Fact]
    public void ProblemScore_OhneValidierung_IstNull()
    {
        Assert.Equal(0, KbDashboardService.ComputeProblemScore(0, null));
        Assert.Equal(0, KbDashboardService.ComputeProblemScore(0, 0.5));
    }

    [Fact]
    public void ProblemScore_HoheAccuracy_IstNiedrig()
    {
        var score95 = KbDashboardService.ComputeProblemScore(100, 0.95);
        var score30 = KbDashboardService.ComputeProblemScore(100, 0.30);

        Assert.True(score30 > score95,
            "Code mit niedriger Accuracy muss hoeheren ProblemScore haben.");
    }

    [Fact]
    public void ProblemScore_VieleStichproben_GewichtetStaerker()
    {
        // Gleiche Accuracy, aber unterschiedliche Stichprobenzahl.
        var score5 = KbDashboardService.ComputeProblemScore(5, 0.4);
        var score200 = KbDashboardService.ComputeProblemScore(200, 0.4);

        Assert.True(score200 > score5,
            "Mehr Stichproben bei gleicher Accuracy → hoeherer ProblemScore.");
    }

    // ── BuildSnapshot ───────────────────────────────────────────────

    [Fact]
    public void BuildSnapshot_KombiniertSamples_Validierung_Quality()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);

        // Samples: BAB hat 100, BAC hat 5 (unterrepraesentiert), BBA hat 80
        for (int i = 0; i < 100; i++)
            InsertSample(ctx, $"bab_{i}", code: "BAB", quality: i < 30 ? "Green" : "Yellow");
        for (int i = 0; i < 5; i++)
            InsertSample(ctx, $"bac_{i}", code: "BAC", quality: "Red");
        for (int i = 0; i < 80; i++)
            InsertSample(ctx, $"bba_{i}", code: "BBA", quality: "Green");

        // Validierungen: BAB ist gut (90%), BAC ist schlecht (20%)
        for (int i = 0; i < 9; i++) InsertValidation(ctx, "BAB", correct: true);
        InsertValidation(ctx, "BAB", correct: false);
        for (int i = 0; i < 8; i++) InsertValidation(ctx, "BAC", correct: false);
        for (int i = 0; i < 2; i++) InsertValidation(ctx, "BAC", correct: true);

        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var dashboard = new KbDashboardService(diag, () => 42)
        {
            UnderRepresentedThreshold = 50,
            MinValidationsForProblemRanking = 5
        };
        var snap = dashboard.BuildSnapshot();

        Assert.Equal(185, snap.TotalSamples);
        Assert.Equal(20, snap.TotalValidations);
        Assert.NotNull(snap.OverallAccuracy);
        Assert.Equal(11.0 / 20.0, snap.OverallAccuracy!.Value, 3);

        Assert.Equal(110, snap.Quality.Green);
        Assert.Equal(70, snap.Quality.Yellow);
        Assert.Equal(5, snap.Quality.Red);
        Assert.Equal(0, snap.Quality.Unknown);

        // BAC muss als Top-Problem oben stehen (niedrige Accuracy + ausreichend Stichproben)
        Assert.NotEmpty(snap.TopProblemCodes);
        Assert.Equal("BAC", snap.TopProblemCodes[0].VsaCode);

        // BAC ist auch unterrepraesentiert (5 Samples < 50)
        Assert.Contains(snap.UnderRepresentedCodes, c => c.VsaCode == "BAC");
        Assert.DoesNotContain(snap.UnderRepresentedCodes, c => c.VsaCode == "BAB");

        Assert.Equal(42, snap.ReviewQueueLength);
    }

    [Fact]
    public void BuildSnapshot_FiltertCodesUnterMinValidations_AusTopProblems()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        InsertSample(ctx, "s1", code: "BAB", quality: "Green");
        // Nur 2 Validierungen — soll NICHT in Top-Problems erscheinen.
        InsertValidation(ctx, "BAB", correct: false);
        InsertValidation(ctx, "BAB", correct: false);

        var diag = new KnowledgeBaseDiagnosticsService(ctx);
        var dashboard = new KbDashboardService(diag) { MinValidationsForProblemRanking = 5 };
        var snap = dashboard.BuildSnapshot();

        Assert.Empty(snap.TopProblemCodes);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static void InsertSample(KnowledgeBaseContext ctx, string sampleId,
        string code, string? quality)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Samples (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                                 IsStreck, FramePath, ExportedUtc, VersionId, QualityGateLevel)
            VALUES ($id, 'case_x', $code, '', 0, 0, 0, '', '2026-05-08T00:00:00Z', 'v1', $q)
            """;
        cmd.Parameters.AddWithValue("$id", sampleId);
        cmd.Parameters.AddWithValue("$code", code);
        cmd.Parameters.AddWithValue("$q", (object?)quality ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static void InsertValidation(
        KnowledgeBaseContext ctx,
        string code,
        bool correct,
        string? suggestedCode = null,
        string? finalCode = null)
    {
        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ValidationLog (LogId, VsaCode, SuggestedCode, FinalCode, WasCorrect,
                                       EvidenceJson, CreatedUtc)
            VALUES ($id, $code, $suggested, $final, $c, '{}', $utc)
            """;
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$code", code);
        cmd.Parameters.AddWithValue("$suggested", suggestedCode ?? code);
        cmd.Parameters.AddWithValue("$final", finalCode ?? code);
        cmd.Parameters.AddWithValue("$c", correct ? 1 : 0);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }
}
