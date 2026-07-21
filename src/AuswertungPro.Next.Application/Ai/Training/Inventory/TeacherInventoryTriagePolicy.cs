namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public static partial class TeacherInventoryPolicy
{
    public static TrainingInventoryEvalState ClassifyEvalState(
        TrainingInventoryPathReference fullFrame,
        string? holdingName,
        bool evalChecksEnabled,
        bool evalProtectionComplete,
        IReadOnlySet<string> evalImageHashes,
        IReadOnlySet<string> evalHoldingKeys)
    {
        var imageMatch = evalChecksEnabled
                         && fullFrame.Sha256 is not null
                         && evalImageHashes.Contains(fullFrame.Sha256);
        var protectedPath = fullFrame.IsProtected;
        var holdingKey = EvalContaminationGuard.NormalizeHaltungKey(holdingName);
        var holdingMatch = holdingKey is not null && evalHoldingKeys.Contains(holdingKey);

        if (imageMatch && holdingMatch)
            return TrainingInventoryEvalState.ImageHashAndHolding;
        if (imageMatch)
            return TrainingInventoryEvalState.ImageHash;
        if (protectedPath && holdingMatch)
            return TrainingInventoryEvalState.ProtectedPathAndHolding;
        if (protectedPath)
            return TrainingInventoryEvalState.ProtectedPath;
        if (holdingMatch)
            return TrainingInventoryEvalState.Holding;
        if (!evalChecksEnabled)
            return TrainingInventoryEvalState.NotChecked;
        if (!evalProtectionComplete)
            return TrainingInventoryEvalState.NotChecked;
        if (fullFrame.Exists && fullFrame.HashState != TrainingInventoryHashState.Computed)
            return TrainingInventoryEvalState.NotChecked;
        return TrainingInventoryEvalState.Clean;
    }

    public static TrainingInventoryDisposition ClassifyDisposition(
        TrainingInventoryPathReference fullFrame,
        TrainingInventoryHoldingState holdingState,
        TrainingInventoryBoxState boxState,
        TrainingInventoryEvalState evalState)
    {
        if (!fullFrame.Exists || boxState == TrainingInventoryBoxState.MissingOrNonPositiveArea)
            return TrainingInventoryDisposition.Archive;
        if (evalState is not TrainingInventoryEvalState.Clean
            and not TrainingInventoryEvalState.NotChecked)
        {
            return TrainingInventoryDisposition.EvaluationLocked;
        }
        if (holdingState != TrainingInventoryHoldingState.Explicit)
            return TrainingInventoryDisposition.QuarantineOrigin;
        if (boxState != TrainingInventoryBoxState.Valid)
            return TrainingInventoryDisposition.QuarantineGeometry;
        return evalState == TrainingInventoryEvalState.Clean
            ? TrainingInventoryDisposition.TrainValCandidate
            : TrainingInventoryDisposition.EvaluationNotChecked;
    }
}
