using System;
using System.IO;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Audit P0-2: LearnedWeightsGateFactory muss die gelernten CategoryWeights aus der KB laden
/// und im Gate aktivieren — und bei fehlender DB/leeren Gewichten verhaltensneutral auf die
/// Default-Gewichte zurueckfallen.
/// </summary>
public sealed class LearnedWeightsGateFactoryTests
{
    [Fact]
    public void Create_OhneGelernteGewichte_VerhaeltSichWieDefaultGate()
    {
        var evidence = new EvidenceVector(YoloConf: 0.2, LlmCodeConf: 0.9, DamageCategory: "BAB");
        var expected = new QualityGateService().Evaluate(evidence).CompositeConfidence;

        var root = Path.Combine(Path.GetTempPath(), "gate-factory", Guid.NewGuid().ToString("N"));
        try
        {
            // Frische, leere DB -> keine gelernten Gewichte -> exakt Default-Verhalten.
            var gate = LearnedWeightsGateFactory.Create(Path.Combine(root, "kb.db"));
            var actual = gate.Evaluate(evidence).CompositeConfidence;
            Assert.Equal(expected, actual, precision: 6);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_MitGelerntenGewichten_AktiviertSieImGate()
    {
        var root = Path.Combine(Path.GetTempPath(), "gate-factory", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "kb.db");
        try
        {
            // Extreme gelernte Gewichte fuer "BAB": alles Gewicht auf YOLO, nichts auf LLM.
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var learned = new CategoryWeights { Category = "BAB", ValidationCount = 50 };
                learned.FromArray(new double[] { 1, 0, 0, 0, 0, 0, 0, 0 }); // nur WYolo
                using var cmd = db.Connection.CreateCommand();
                cmd.CommandText =
                    "INSERT OR REPLACE INTO CategoryWeights (Category, WeightsJson, ValidationCount, UpdatedUtc) " +
                    "VALUES (@c, @j, @v, @u)";
                cmd.Parameters.AddWithValue("@c", "BAB");
                cmd.Parameters.AddWithValue("@j", learned.ToJson());
                cmd.Parameters.AddWithValue("@v", 50);
                cmd.Parameters.AddWithValue("@u", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }

            // Zwei Signale (YOLO niedrig, LLM hoch) -> die Gewichtung entscheidet das Ergebnis.
            var evidence = new EvidenceVector(YoloConf: 0.2, LlmCodeConf: 0.9, DamageCategory: "BAB");

            var defaultComposite = new QualityGateService().Evaluate(evidence).CompositeConfidence;
            var learnedComposite = LearnedWeightsGateFactory.Create(dbPath).Evaluate(evidence).CompositeConfidence;

            // Default gewichtet LLM (0.9) staerker -> hoher Composite. Gelernt zaehlt fast nur
            // YOLO (0.2) -> deutlich niedriger. Der Unterschied beweist: Gewichte sind aktiv.
            Assert.True(learnedComposite < defaultComposite - 0.1,
                $"Gelernte Gewichte nicht aktiv: default={defaultComposite:F3}, learned={learnedComposite:F3}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
