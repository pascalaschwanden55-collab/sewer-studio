namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Erzeugt die gruppierte, widerspruchsfreie Zusammenfassung.</summary>
public static class TrainingInventorySummaryBuilder
{
    public static TrainingDataInventorySummary Build(
        IReadOnlyList<TeacherInventoryRecord> records,
        IReadOnlyList<TrainingInventorySourceDocument> sources)
        => new()
        {
            Data = new TrainingInventoryDataFactsSummary
            {
                TeacherRecords = records.Count,
                ExistingFullFrames = records.Count(record => record.FullFrame.Exists),
                PositiveAreaBoxes = records.Count(record => record.HasPositiveArea),
                StrictlyValidBoxes = records.Count(record => record.BoxState == TrainingInventoryBoxState.Valid),
                ExistingFrameAndPositiveArea = records.Count(record =>
                    record.HasPositiveArea && record.FullFrame.Exists),
                ExistingFrameAndStrictlyValidBox = records.Count(record =>
                    record.BoxState == TrainingInventoryBoxState.Valid && record.FullFrame.Exists)
            },
            Holdings = new TrainingInventoryHoldingSummary
            {
                Explicit = records.Count(record => record.HoldingState == TrainingInventoryHoldingState.Explicit),
                NonExplicit = records.Count(record => record.HoldingState != TrainingInventoryHoldingState.Explicit),
                ExistingFramePositiveAreaExplicit = records.Count(record =>
                    record.HasPositiveArea
                    && record.HoldingState == TrainingInventoryHoldingState.Explicit
                    && record.FullFrame.Exists)
            },
            Triage = new TrainingInventoryTriageSummary
            {
                TrainValCandidates = CountDisposition(records, TrainingInventoryDisposition.TrainValCandidate),
                QuarantineOrigin = CountDisposition(records, TrainingInventoryDisposition.QuarantineOrigin),
                QuarantineGeometry = CountDisposition(records, TrainingInventoryDisposition.QuarantineGeometry),
                Archive = CountDisposition(records, TrainingInventoryDisposition.Archive),
                EvaluationLocked = CountDisposition(records, TrainingInventoryDisposition.EvaluationLocked),
                EvaluationNotChecked = CountDisposition(records, TrainingInventoryDisposition.EvaluationNotChecked)
            },
            Paths = new TrainingInventoryPathSummary
            {
                FullFrameSuggestions = records.Count(record =>
                    record.FullFrame.State == TrainingInventoryPathState.SuggestedForManualReview),
                AmbiguousFullFrameReferences = records.Count(record =>
                    record.FullFrame.State == TrainingInventoryPathState.Ambiguous),
                ReadErrors = records.Sum(CountReadErrors)
            },
            Evaluation = new TrainingInventoryEvalSummary
            {
                ReservedRecords = records.Count(record =>
                    record.EvalState is not TrainingInventoryEvalState.Clean
                        and not TrainingInventoryEvalState.NotChecked),
                UncheckedRecords = records.Count(record =>
                    record.EvalState == TrainingInventoryEvalState.NotChecked)
            },
            Sources = new TrainingInventorySourceSummary
            {
                Documents = sources.Count,
                InvalidDocuments = sources.Count(source =>
                    source.ParseState == TrainingInventoryParseState.Invalid)
            }
        };

    private static int CountDisposition(
        IEnumerable<TeacherInventoryRecord> records,
        TrainingInventoryDisposition disposition)
        => records.Count(record => record.Disposition == disposition);

    private static int CountReadErrors(TeacherInventoryRecord record)
        => new[] { record.FullFrame, record.CroppedRegion, record.YoloAnnotation, record.Video }
            .Count(path => path.HashState == TrainingInventoryHashState.ReadError
                           || path.State == TrainingInventoryPathState.Invalid);
}
