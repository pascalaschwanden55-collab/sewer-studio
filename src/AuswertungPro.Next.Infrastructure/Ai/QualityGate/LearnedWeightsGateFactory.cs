using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

/// <summary>
/// Baut das QualityGate. Gelernte Gewichte bleiben standardmaessig im Schattenbetrieb:
/// Ohne getrennte Validierung duerfen sie produktive Freigaben nicht veraendern.
/// </summary>
public static class LearnedWeightsGateFactory
{
    /// <summary>
    /// Erstellt ein QualityGate. Experimentelle Gewichte werden nur nach einer
    /// ausdruecklichen Aktivierung geladen.
    /// </summary>
    /// <param name="dbPath">Optionaler DB-Pfad (fuer Tests); sonst die Standard-KnowledgeBase.</param>
    public static QualityGateService Create(
        string? dbPath = null,
        bool activateExperimentalWeights = false)
    {
        var gate = new QualityGateService();
        if (activateExperimentalWeights)
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
