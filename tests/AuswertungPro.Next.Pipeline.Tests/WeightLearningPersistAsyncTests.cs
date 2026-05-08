using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 2.2: Tests fuer den expliziten Persistierungs-Pfad des
/// <see cref="WeightLearningService"/>. Prueft:
///  1) PersistAsync schreibt alle Pending-Categories in die getypten Spalten.
///  2) Noise-Filter (L1 &lt;= NoiseFilterThreshold) verhindert UPSERT.
/// </summary>
[Trait("Category", "Integration")]
public sealed class WeightLearningPersistAsyncTests : IDisposable
{
    private readonly string _dbPath;

    public WeightLearningPersistAsyncTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"sewer_kb_persist_{Guid.NewGuid():N}.db");
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

    [Fact]
    public async Task PersistAsync_SchreibtAlleCategoriesInDieTabelle()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var svc = new WeightLearningService(ctx.Connection);

        // 8 Gewichte in kanonischer Reihenfolge:
        // Yolo, Dino, Sam, Qwen, Llm, Kb, KbAgreement, Plausibility
        var wA = new[] { 0.10, 0.20, 0.05, 0.15, 0.10, 0.15, 0.10, 0.15 };
        var wB = new[] { 0.05, 0.05, 0.30, 0.05, 0.20, 0.10, 0.10, 0.15 };

        svc.SetPendingForTest("BAB", wA, validationCount: 42);
        svc.SetPendingForTest("BBB", wB, validationCount: 30);

        var written = await svc.PersistAsync();

        Assert.Equal(2, written);

        // Verifizieren: beide Zeilen existieren mit den getypten Spalten
        AssertRow(ctx.Connection, "BAB", wA, expectedCount: 42);
        AssertRow(ctx.Connection, "BBB", wB, expectedCount: 30);
    }

    [Fact]
    public async Task PersistAsync_SkipsRowWennDifferenzKleinerNoiseFilter()
    {
        using var ctx = new KnowledgeBaseContext(_dbPath);
        var svc = new WeightLearningService(ctx.Connection)
        {
            // expliziter Threshold fuer Klarheit (Default ist ohnehin 0.01)
            NoiseFilterThreshold = 0.01
        };

        var initial = new[] { 0.10, 0.20, 0.05, 0.15, 0.10, 0.15, 0.10, 0.15 };

        // 1) Erster Persist — Tabelle ist leer, schreibt durch.
        svc.SetPendingForTest("BAB", initial, validationCount: 42);
        var firstWritten = await svc.PersistAsync();
        Assert.Equal(1, firstWritten);

        // 2) Mini-Drift unter NoiseFilter (L1-Diff = 0.004, Schwelle 0.01).
        //    Aenderung nur an einer Dimension um 0.004 -> L1 = 0.004 < 0.01.
        var nearlyEqual = (double[])initial.Clone();
        nearlyEqual[0] += 0.004;

        svc.SetPendingForTest("BAB", nearlyEqual, validationCount: 99);
        var secondWritten = await svc.PersistAsync();
        Assert.Equal(0, secondWritten);

        // ValidationCount darf NICHT auf 99 hochgeschrieben sein, da skip.
        AssertRow(ctx.Connection, "BAB", initial, expectedCount: 42);

        // 3) Echte Aenderung ueber dem Noise-Threshold -> wird geschrieben.
        var changed = (double[])initial.Clone();
        changed[1] += 0.05; // L1 = 0.05 > 0.01

        svc.SetPendingForTest("BAB", changed, validationCount: 100);
        var thirdWritten = await svc.PersistAsync();
        Assert.Equal(1, thirdWritten);

        AssertRow(ctx.Connection, "BAB", changed, expectedCount: 100);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void AssertRow(
        SqliteConnection conn, string category, double[] expected, int expectedCount)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT YoloWeight, DinoWeight, SamWeight, QwenWeight,
                   LlmWeight, KbWeight, KbAgreementWeight, PlausibilityWeight,
                   ValidationCount, WeightsJson
            FROM CategoryWeights
            WHERE Category = @cat
            """;
        cmd.Parameters.AddWithValue("@cat", category);
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read(), $"Zeile fuer '{category}' nicht gefunden.");

        for (int i = 0; i < 8; i++)
        {
            var actual = reader.GetDouble(i);
            Assert.True(
                Math.Abs(actual - expected[i]) < 1e-9,
                $"Spalte #{i} erwartet {expected[i]}, ist {actual}.");
        }

        Assert.Equal(expectedCount, reader.GetInt32(8));

        // JSON-Fallback muss parallel geschrieben sein (Pre-2.2-Reader).
        var json = reader.IsDBNull(9) ? null : reader.GetString(9);
        Assert.False(string.IsNullOrWhiteSpace(json),
            "WeightsJson muss parallel zur getypten Speicherung geschrieben sein.");
    }
}

/// <summary>
/// Test-Helfer um den internen Pending-State direkt zu setzen, ohne dass
/// echte Validation-Daten erzeugt werden muessten. Greift auf die
/// internal-Methode <c>SetPending</c> via InternalsVisibleTo zu — falls
/// kein InternalsVisibleTo gesetzt ist, nutzen wir Reflection als Fallback.
/// </summary>
internal static class WeightLearningTestExtensions
{
    public static void SetPendingForTest(
        this WeightLearningService svc, string category, double[] weights, int validationCount)
    {
        // Reflection — vermeidet Aenderung an InternalsVisibleTo nur fuer Tests.
        var mi = typeof(WeightLearningService).GetMethod(
            "SetPending",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(mi);
        mi!.Invoke(svc, new object[] { category, weights, validationCount });
    }
}
