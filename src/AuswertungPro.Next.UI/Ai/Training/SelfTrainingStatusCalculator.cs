using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Reine Berechnungen fuer die Self-Training-Statusanzeige im TrainingCenter:
/// Match-Rate-Prozente, Stage-Modellanzeige, Level-Beschriftung und das
/// Inkrement der Code-Verteilung. Kein UI, kein Dispatcher, kein State.
/// </summary>
public static class SelfTrainingStatusCalculator
{
    /// <summary>Prozentanteile (0..1) der Match-Level. Bei 0 Faellen alle 0.</summary>
    public readonly record struct MatchRatePercents(double Exact, double Partial, double Mismatch, double NoFindings);

    /// <summary>Berechnet die Match-Rate-Anteile aus den absoluten Zaehlern.</summary>
    public static MatchRatePercents ComputeMatchRatePercents(int exact, int partial, int mismatch, int noFindings)
    {
        var total = exact + partial + mismatch + noFindings;
        if (total == 0)
            return new MatchRatePercents(0, 0, 0, 0);

        return new MatchRatePercents(
            (double)exact / total,
            (double)partial / total,
            (double)mismatch / total,
            (double)noFindings / total);
    }

    /// <summary>
    /// Liefert die Anzeige (Modell-Label, aktiv?) fuer eine Pipeline-Stage.
    /// Bei GPU-Stages wird das aktive Vision-Modell eingeblendet.
    /// </summary>
    public static (string ModelLabel, bool IsActive) ResolveActiveModel(SelfTrainingStage stage, string activeVisionModel)
        => stage switch
        {
            SelfTrainingStage.BuildingTimeline => ("PdfPig (CPU)", true),
            SelfTrainingStage.ExtractingFrame => ("ffmpeg (CPU)", true),
            SelfTrainingStage.Analyzing => ($"{activeVisionModel} (GPU)", true),
            SelfTrainingStage.Comparing => ("Deterministisch (CPU)", true),
            SelfTrainingStage.AssessingTechnique => ($"{activeVisionModel} (GPU)", true),
            SelfTrainingStage.Completed => ("", false),
            _ => ("", false)
        };

    /// <summary>Kurzlabel eines Match-Levels fuer Log/Anzeige.</summary>
    public static string FormatLevel(MatchLevel level)
        => level switch
        {
            MatchLevel.ExactMatch => "EXACT",
            MatchLevel.PartialMatch => "PARTIAL",
            MatchLevel.Mismatch => "MISMATCH",
            _ => "NO_FINDINGS"
        };

    /// <summary>Erhoeht den Gesamt- und den zum Level passenden Zaehler eines Code-Verteilungs-Eintrags.</summary>
    public static void ApplyMatch(CodeDistributionEntry entry, MatchLevel level)
    {
        entry.Total++;
        switch (level)
        {
            case MatchLevel.ExactMatch: entry.Exact++; break;
            case MatchLevel.PartialMatch: entry.Partial++; break;
            case MatchLevel.Mismatch: entry.Mismatch++; break;
            case MatchLevel.NoFindings: entry.NoFindings++; break;
        }
    }
}
