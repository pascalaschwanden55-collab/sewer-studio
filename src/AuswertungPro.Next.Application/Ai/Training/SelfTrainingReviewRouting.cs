namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Zentrale, reine Regel: welcher Self-Training-Befund kommt in die Review-Queue und mit welcher
/// Prioritaet. KI-Fehler zuerst (NoFindings = uebersehener Schaden, Mismatch = falscher Code).
/// </summary>
public static class SelfTrainingReviewRouting
{
    public static bool ShouldEnqueue(MatchLevel level, TrainingSampleStatus status)
    {
        if (status is TrainingSampleStatus.Approved or TrainingSampleStatus.Rejected or TrainingSampleStatus.Removed)
            return false;
        return level is MatchLevel.NoFindings or MatchLevel.Mismatch
                     or MatchLevel.PartialMatch or MatchLevel.ExactMatch;
    }

    public static double Priority(MatchLevel level) => level switch
    {
        MatchLevel.NoFindings => 0.95,
        MatchLevel.Mismatch => 0.90,
        MatchLevel.PartialMatch => 0.60,
        _ => 0.30,
    };
}
