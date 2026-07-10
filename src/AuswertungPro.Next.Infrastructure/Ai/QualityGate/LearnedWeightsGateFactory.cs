using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

/// <summary>
/// Baut einen <see cref="QualityGateService"/> und laedt dabei die vom
/// <see cref="WeightLearningService"/> gelernten, persistierten CategoryWeights aus der
/// KnowledgeBase (Audit P0-2). Ohne diese Bruecke lief jedes Gate dauerhaft mit
/// <c>CategoryWeights.Default()</c>, egal wie viele Feedbacks gelernt wurden — der
/// halb-offene Lernkreis (siehe ADR-008) wird hiermit geschlossen.
///
/// Robust: Faellt bei fehlender/gesperrter DB oder leerer Gewichtstabelle still auf
/// Default-Gewichte zurueck. Ein QualityGateService ohne gesetzte Gewichte verhaelt sich
/// exakt wie bisher, d.h. dieser Fallback ist verhaltensneutral.
/// </summary>
public static class LearnedWeightsGateFactory
{
    /// <summary>
    /// Erstellt einen QualityGateService und aktiviert alle gelernten CategoryWeights.
    /// </summary>
    /// <param name="dbPath">Optionaler DB-Pfad (fuer Tests); sonst die Standard-KnowledgeBase.</param>
    public static QualityGateService Create(string? dbPath = null)
    {
        var gate = new QualityGateService();
        LoadInto(gate, dbPath);
        return gate;
    }

    /// <summary>
    /// Laedt gelernte Gewichte in ein bestehendes Gate (fuer Faelle, in denen das Gate
    /// bereits anderweitig erzeugt wurde). Idempotent pro Kategorie (SetWeights ueberschreibt).
    /// </summary>
    public static void LoadInto(QualityGateService gate, string? dbPath = null)
    {
        ArgumentNullException.ThrowIfNull(gate);
        try
        {
            using var db = new KnowledgeBaseContext(dbPath);
            var learner = new WeightLearningService(db.Connection);
            foreach (var weights in learner.LoadAllWeights())
                gate.SetWeights(weights);
        }
        catch (Exception ex)
        {
            // Gelernte Gewichte sind eine Optimierung, kein Muss: bei Fehler bleibt das
            // Default-Verhalten erhalten (kein Absturz der Pipeline wegen DB-Problem).
            System.Diagnostics.Debug.WriteLine(
                $"[LearnedWeightsGateFactory] Gewichte nicht geladen, nutze Default: {ex.Message}");
        }
    }
}
