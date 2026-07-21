namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public static class TrainingInventoryReasonCodes
{
    public const string FullFramePathSuggestion = "full-frame-path-suggestion";
    public const string FullFramePathAmbiguous = "full-frame-path-ambiguous";
    public const string FullFrameCandidateProtected = "full-frame-candidate-protected";
    public const string FullFrameMissing = "full-frame-missing";
    public const string FullFramePathInvalid = "full-frame-path-invalid";
    public const string FullFrameHashReadError = "full-frame-hash-read-error";
    public const string PositiveBoxMissing = "positive-box-missing";
    public const string BoxCoordinatesNonFinite = "box-coordinates-non-finite";
    public const string BoxValuesOutsideNormalizedRange = "box-values-outside-normalized-range";
    public const string BoxExtendsOutsideImage = "box-extends-outside-image";
    public const string HoldingSuggestionNeedsReview = "holding-suggestion-needs-review";
    public const string HoldingAmbiguous = "holding-ambiguous";
    public const string HoldingUnknown = "holding-unknown";
    public const string EvaluationLocked = "evaluation-locked";
    public const string EvaluationNotChecked = "evaluation-not-checked";
    public const string TriageTrainValCandidate = "triage-train-val-candidate";
    public const string TriageQuarantineOrigin = "triage-quarantine-origin";
    public const string TriageQuarantineGeometry = "triage-quarantine-geometry";
    public const string TriageEvaluationLocked = "triage-evaluation-locked";
    public const string TriageEvaluationNotChecked = "triage-evaluation-not-checked";
    public const string TriageArchive = "triage-archive";
}
