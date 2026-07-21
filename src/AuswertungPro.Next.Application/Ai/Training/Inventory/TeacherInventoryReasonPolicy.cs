namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public static partial class TeacherInventoryPolicy
{
    public static IReadOnlyList<string> BuildReasonCodes(
        TrainingInventoryPathReference fullFrame,
        TrainingInventoryBoxState boxState,
        TeacherInventoryHoldingAssessment holding,
        TrainingInventoryEvalState evalState,
        TrainingInventoryDisposition disposition)
    {
        var reasons = new List<string>();
        AddPathReasons(fullFrame, reasons);
        AddBoxReason(boxState, reasons);

        if (!holding.IsExplicit)
        {
            reasons.Add(holding.State switch
            {
                TrainingInventoryHoldingState.SuggestionNeedsManualReview => TrainingInventoryReasonCodes.HoldingSuggestionNeedsReview,
                TrainingInventoryHoldingState.Ambiguous => TrainingInventoryReasonCodes.HoldingAmbiguous,
                _ => TrainingInventoryReasonCodes.HoldingUnknown
            });
        }

        if (evalState is not TrainingInventoryEvalState.Clean
            and not TrainingInventoryEvalState.NotChecked)
        {
            reasons.Add(TrainingInventoryReasonCodes.EvaluationLocked);
        }
        else if (evalState == TrainingInventoryEvalState.NotChecked)
        {
            reasons.Add(TrainingInventoryReasonCodes.EvaluationNotChecked);
        }

        reasons.Add(disposition switch
        {
            TrainingInventoryDisposition.TrainValCandidate => TrainingInventoryReasonCodes.TriageTrainValCandidate,
            TrainingInventoryDisposition.QuarantineOrigin => TrainingInventoryReasonCodes.TriageQuarantineOrigin,
            TrainingInventoryDisposition.QuarantineGeometry => TrainingInventoryReasonCodes.TriageQuarantineGeometry,
            TrainingInventoryDisposition.EvaluationLocked => TrainingInventoryReasonCodes.TriageEvaluationLocked,
            TrainingInventoryDisposition.EvaluationNotChecked => TrainingInventoryReasonCodes.TriageEvaluationNotChecked,
            _ => TrainingInventoryReasonCodes.TriageArchive
        });
        return reasons;
    }

    private static void AddPathReasons(
        TrainingInventoryPathReference fullFrame,
        ICollection<string> reasons)
    {
        switch (fullFrame.State)
        {
            case TrainingInventoryPathState.SuggestedForManualReview:
                reasons.Add(TrainingInventoryReasonCodes.FullFramePathSuggestion);
                break;
            case TrainingInventoryPathState.Ambiguous:
                reasons.Add(TrainingInventoryReasonCodes.FullFramePathAmbiguous);
                break;
            case TrainingInventoryPathState.ProtectedCandidate:
                reasons.Add(TrainingInventoryReasonCodes.FullFrameCandidateProtected);
                break;
            case TrainingInventoryPathState.Empty:
            case TrainingInventoryPathState.Missing:
                reasons.Add(TrainingInventoryReasonCodes.FullFrameMissing);
                break;
            case TrainingInventoryPathState.Invalid:
                reasons.Add(TrainingInventoryReasonCodes.FullFramePathInvalid);
                break;
        }

        if (fullFrame.HashState == TrainingInventoryHashState.ReadError)
            reasons.Add(TrainingInventoryReasonCodes.FullFrameHashReadError);
    }

    private static void AddBoxReason(
        TrainingInventoryBoxState state,
        ICollection<string> reasons)
    {
        switch (state)
        {
            case TrainingInventoryBoxState.MissingOrNonPositiveArea:
                reasons.Add(TrainingInventoryReasonCodes.PositiveBoxMissing);
                break;
            case TrainingInventoryBoxState.NonFiniteCoordinates:
                reasons.Add(TrainingInventoryReasonCodes.BoxCoordinatesNonFinite);
                break;
            case TrainingInventoryBoxState.PositiveOutOfNormalizedRange:
                reasons.Add(TrainingInventoryReasonCodes.BoxValuesOutsideNormalizedRange);
                break;
            case TrainingInventoryBoxState.ExtendsOutsideImage:
                reasons.Add(TrainingInventoryReasonCodes.BoxExtendsOutsideImage);
                break;
        }
    }
}
