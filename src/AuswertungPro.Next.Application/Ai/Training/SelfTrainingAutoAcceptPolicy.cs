namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Entscheidet, ob ein Self-Training-Sample automatisch als Gold-Standard / KB-Wahrheit uebernommen
/// werden darf oder zur menschlichen Pruefung muss. Reine, deterministische Politik (kein I/O).
///
/// Sicherheitsregel (S2b): Bei RequireHumanReview wird KEIN Sample automatisch indexiert — auch ein
/// sauberer 4-Achsen-ExactMatch (S2: Code + Meter + Severity + Uhrlage) bleibt nur Kandidat und geht
/// in die ReviewQueue. Confidence/Margin/KB-Disagreement existieren im LLM-Self-Training-Pfad NICHT
/// und werden bewusst nicht erfunden — ein Fake-Signal waere gefaehrlicher als kein Signal.
/// </summary>
public static class SelfTrainingAutoAcceptPolicy
{
    public const string HumanReviewRequiredReason = "HumanReviewRequired";
    public const string KbDisagreementReason = "KbDisagreement";

    public readonly record struct Decision(
        TrainingSampleStatus Status,
        KbIndexState KbIndexState,
        bool RouteToReview,
        string? Reason);

    public static Decision Decide(
        MatchLevel level,
        bool requireHumanReview,
        KbCheckResult kbCheck = KbCheckResult.KbNoSignal)
    {
        // Weg 1: KB-Mehrheit widerspricht dem KI-Code -> IMMER Review (Veto), unabhaengig von
        // MatchLevel/Flag. KbAgreement/KbNoSignal aendern die Entscheidung NICHT — Agreement ist
        // nur ein Kandidaten-Signal, kein Auto-Gold-Trigger; RequireHumanReview bleibt staerker.
        if (kbCheck == KbCheckResult.KbDisagreement)
            return new Decision(
                TrainingSampleStatus.New, KbIndexState.None,
                RouteToReview: true, KbDisagreementReason);

        bool cleanExact = level == MatchLevel.ExactMatch;

        // Sauberer Treffer, aber Mensch muss bestaetigen -> Kandidat, KEIN Auto-Gold/Index.
        if (cleanExact && requireHumanReview)
            return new Decision(
                TrainingSampleStatus.New, KbIndexState.None,
                RouteToReview: true, HumanReviewRequiredReason);

        // Sauberer Treffer und Auto-Accept erlaubt -> wie S2 (Approve + KB-Index als Pending).
        if (cleanExact)
            return new Decision(
                TrainingSampleStatus.Approved, KbIndexState.Pending,
                RouteToReview: false, Reason: null);

        // Kein sauberer Treffer -> ohnehin kein Gold, geht in Review.
        return new Decision(
            TrainingSampleStatus.New, KbIndexState.None,
            RouteToReview: true, Reason: null);
    }
}
