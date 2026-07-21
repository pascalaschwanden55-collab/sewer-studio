namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public enum TrainingInventoryDataKind
{
    TeacherAnnotations,
    TrainingSamples
}

public enum TrainingInventorySourceRole
{
    Current,
    Backup,
    Legacy
}

public enum TrainingInventoryParseState
{
    Parsed,
    Missing,
    Invalid
}

public enum TrainingInventoryValidationLevel
{
    JsonArray,
    TypedRecords
}

public enum TrainingInventoryPathState
{
    Empty,
    Existing,
    SuggestedForManualReview,
    Ambiguous,
    ProtectedCandidate,
    Missing,
    Invalid
}

public enum TrainingInventoryHashState
{
    NotRequested,
    NotApplicable,
    Computed,
    ReadError
}

public enum TrainingInventoryBoxState
{
    MissingOrNonPositiveArea,
    NonFiniteCoordinates,
    PositiveOutOfNormalizedRange,
    ExtendsOutsideImage,
    Valid
}

public enum TrainingInventoryHoldingState
{
    Explicit,
    Unknown,
    SuggestionNeedsManualReview,
    Ambiguous
}

public enum TrainingInventoryDisposition
{
    TrainValCandidate,
    QuarantineOrigin,
    QuarantineGeometry,
    Archive,
    EvaluationLocked,
    EvaluationNotChecked
}

public enum TrainingInventoryEvalState
{
    Clean,
    ImageHash,
    Holding,
    ImageHashAndHolding,
    ProtectedPath,
    ProtectedPathAndHolding,
    NotChecked
}

public enum TrainingInventoryIssueSeverity
{
    Warning,
    Error
}
