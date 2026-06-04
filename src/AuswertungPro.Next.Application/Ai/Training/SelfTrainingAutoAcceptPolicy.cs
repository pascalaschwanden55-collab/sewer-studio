namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Entscheidet deterministisch, ob ein Self-Training-Sample automatisch als Gold-Standard / KB-Wahrheit
/// uebernommen werden darf oder zur menschlichen Pruefung muss.
/// </summary>
public static class SelfTrainingAutoAcceptPolicy
{
    public const string HumanReviewRequiredReason = "HumanReviewRequired";
    public const string KbDisagreementReason = "KbDisagreement";
    public const string KbAgreementRequiredReason = "KbAgreementRequired";
    public const string ConfidenceInsufficientReason = "ConfidenceInsufficient";
    public const string FramePositionUnverifiedReason = "FramePositionUnverified";

    public readonly record struct Decision(
        TrainingSampleStatus Status,
        KbIndexState KbIndexState,
        bool RouteToReview,
        string? Reason);

    public static Decision Decide(
        MatchLevel level,
        bool requireHumanReview,
        KbCheckResult kbCheck = KbCheckResult.KbNoSignal,
        bool requireKbAgreement = false,
        double confidenceScore = 1.0,
        double confidenceThreshold = 1.0,
        bool framePositionReliable = true)
    {
        if (kbCheck == KbCheckResult.KbDisagreement)
            return Review(KbDisagreementReason);

        bool cleanExact = level == MatchLevel.ExactMatch;
        if (!cleanExact)
            return Review(null);

        if (requireHumanReview)
            return Review(HumanReviewRequiredReason);

        if (confidenceScore < confidenceThreshold)
            return Review(ConfidenceInsufficientReason);

        if (requireKbAgreement && kbCheck != KbCheckResult.KbAgreement)
            return Review(KbAgreementRequiredReason);

        if (!framePositionReliable)
            return Review(FramePositionUnverifiedReason);

        return new Decision(
            TrainingSampleStatus.Approved,
            KbIndexState.Pending,
            RouteToReview: false,
            Reason: null);
    }

    private static Decision Review(string? reason)
        => new(
            TrainingSampleStatus.New,
            KbIndexState.None,
            RouteToReview: true,
            Reason: reason);
}
