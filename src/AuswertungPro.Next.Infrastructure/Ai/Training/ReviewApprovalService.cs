using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Portiert die Self-Training-Review-Logik aus TrainingCenterViewModel.ApplySelfTrainingReviewAsync
/// in einen eigenstaendigen, testbaren Service (§11-Refactor: Verhalten unveraendert).
///
/// Lookup-Strategie: SampleId-Direktsuche statt fragile CaseId/Code/Meter±0.2-Suche.
/// KB-Indexierung wird ueber IKnowledgeBaseIndexer delegiert (erhaelt VM-Methoden via Delegate).
/// UI-Belange (Log, LoadSamplesInternalAsync) bleiben im ViewModel.
/// </summary>
public sealed class ReviewApprovalService : IReviewApprovalService
{
    private readonly ITrainingSampleStore _store;
    private readonly IKnowledgeBaseIndexer _indexer;

    public ReviewApprovalService(ITrainingSampleStore store, IKnowledgeBaseIndexer indexer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
    }

    /// <inheritdoc />
    public async Task<ReviewApplyResult> ApproveSelfTrainingAsync(
        string sampleId,
        BoundingBox? box,
        CancellationToken ct,
        string confirmedByUser,
        TrainingSegmentationMask? mask = null)
    {
        var allSamples = await _store.LoadAsync().ConfigureAwait(false);
        var match = allSamples.FirstOrDefault(s => s.SampleId == sampleId);

        if (match is null)
            return new ReviewApplyResult(Found: false, Indexed: false, Deindexed: false, CorrectedSampleId: null);

        // Optionale BoundingBox VOR Status-Aenderung setzen (Spezifikation: box.Value.ApplyTo(match) BEFORE setting status)
        if (box.HasValue)
            box.Value.ApplyTo(match);
        if (mask is not null)
            mask.ApplyTo(match);

        match.Status = TrainingSampleStatus.Approved;
        match.KbIndexState = KbIndexState.Pending;
        match.MatchLevel = MatchLevelNames.ReviewApproved;

        // Gold-Fund: Review-Queue-Bestaetigung = menschlich bestaetigt (nicht korrigiert)
        match.HumanConfirmed = true;
        match.Corrected = false;
        match.ConfirmedByUser = confirmedByUser;
        match.ConfirmedAtUtc = DateTime.UtcNow;

        // Inkrementell in KB indexieren
        var outcome = await _indexer.IndexAsync(
            new List<TrainingSample> { match }, ct).ConfigureAwait(false);
        match.KbIndexState = outcome.IsIndexed(match.SampleId)
            ? KbIndexState.Indexed
            : outcome.IsSkipped(match.SampleId)
                ? KbIndexState.Skipped   // bewusst/dauerhaft verworfen, kein Fehler
                : KbIndexState.Error;

        // Status-Aenderung atomar speichern
        await _store.MergeOrUpdateAsync(new List<TrainingSample> { match }).ConfigureAwait(false);

        return new ReviewApplyResult(
            Found: true,
            Indexed: match.KbIndexState == KbIndexState.Indexed,
            Deindexed: false,
            CorrectedSampleId: null);
    }

    /// <inheritdoc />
    public async Task<ReviewApplyResult> RejectSelfTrainingAsync(
        string sampleId,
        string? correctedCode,
        CancellationToken ct,
        string confirmedByUser,
        string? correctedDescription = null)
    {
        var allSamples = await _store.LoadAsync().ConfigureAwait(false);
        var match = allSamples.FirstOrDefault(s => s.SampleId == sampleId);

        if (match is null)
            return new ReviewApplyResult(Found: false, Indexed: false, Deindexed: false, CorrectedSampleId: null);

        // Originalen Code sichern BEVOR match.Code moeglicherweise veraendert wird
        var originalCode = match.Code;

        match.Status = TrainingSampleStatus.Rejected;
        match.KbIndexState = KbIndexState.None;

        // T3-Invariante: Ablehnen raeumt KB-Eintrag weg — auch im Review-Queue-Pfad
        _indexer.Deindex(match.SampleId);

        // Gold-Fund: abgelehnt = nicht bestaetigt, Bearbeiter dennoch dokumentieren
        match.HumanConfirmed = false;   // abgelehnt = nicht bestaetigt
        match.Corrected = false;
        match.ConfirmedByUser = confirmedByUser;
        match.ConfirmedAtUtc = DateTime.UtcNow;

        string? correctedSampleId = null;

        if (!string.IsNullOrEmpty(correctedCode))
        {
            match.Notes = $"Korrigiert: {originalCode} → {correctedCode}";

            // Korrigiertes Sample als neues Trainingsbeispiel erzeugen
            // vsaCode in Notes/Signature = der ORIGINALE match.Code (originalCode)
            var corrected = new TrainingSample
            {
                SampleId = $"{match.SampleId}_corr",
                CaseId = match.CaseId,
                Code = correctedCode,
                Beschreibung = string.IsNullOrWhiteSpace(correctedDescription)
                    ? match.Beschreibung
                    : correctedDescription.Trim(),
                MeterStart = match.MeterStart,
                MeterEnd = match.MeterEnd,
                IsStreckenschaden = match.IsStreckenschaden,
                TimeSeconds = match.TimeSeconds,
                DetectedMeter = match.DetectedMeter,
                MeterSource = match.MeterSource,
                FramePath = match.FramePath,
                Status = TrainingSampleStatus.Approved,
                KbIndexState = KbIndexState.Pending,
                TruthMeterCenter = match.TruthMeterCenter,
                OdsDeltaMeters = match.OdsDeltaMeters,
                HasOsdMismatch = match.HasOsdMismatch,
                Signature = TrainingSample.BuildCanonicalSignature(
                    match.CaseId, correctedCode, match.MeterStart, match.MeterEnd),
                MatchLevel = MatchLevelNames.ReviewCorrected,
                SourceType = match.SourceType,
                TechniqueGrade = match.TechniqueGrade,
                KiCode = match.KiCode,
                InspectionDate = match.InspectionDate,
                TrainingEligible = match.TrainingEligible,
                TrainingEligibilityReason = match.TrainingEligibilityReason,
                Notes = $"Korrektur aus Review: {originalCode} → {correctedCode}",
                // Gold-Fund: korrigiertes Sample = bestaetigter, korrigierter Gold-Fund
                HumanConfirmed = true,
                Corrected = true,
                ConfirmedByUser = confirmedByUser,
                ConfirmedAtUtc = DateTime.UtcNow,
                // Box/Maske aus dem Original uebernehmen (duerfen nicht verloren gehen)
                BboxXCenter = match.BboxXCenter,
                BboxYCenter = match.BboxYCenter,
                BboxWidth = match.BboxWidth,
                BboxHeight = match.BboxHeight,
                SamMaskRle = match.SamMaskRle,
                SamMaskImageWidth = match.SamMaskImageWidth,
                SamMaskImageHeight = match.SamMaskImageHeight,
                SamMaskAreaPixels = match.SamMaskAreaPixels,
                SamMaskConfidence = match.SamMaskConfidence,
                SamMaskLabel = match.SamMaskLabel,
            };

            // Korrigiertes Sample per Merge speichern (Race-Condition-sicher)
            await _store.MergeAndSaveAsync(new List<TrainingSample> { corrected }).ConfigureAwait(false);

            // Inkrementell in KB indexieren
            var corrOutcome = await _indexer.IndexAsync(
                new List<TrainingSample> { corrected }, ct).ConfigureAwait(false);
            corrected.KbIndexState = corrOutcome.IsIndexed(corrected.SampleId)
                ? KbIndexState.Indexed
                : corrOutcome.IsSkipped(corrected.SampleId)
                    ? KbIndexState.Skipped
                    : KbIndexState.Error;
            await _store.MergeOrUpdateAsync(new List<TrainingSample> { corrected }).ConfigureAwait(false);

            correctedSampleId = corrected.SampleId;
        }

        // Status-Aenderung von match (Rejected) atomar speichern.
        // MergeOrUpdateAsync statt SaveAsync: verhindert Ueberschreiben von parallel
        // geschriebenen Samples (z.B. corrected Sample).
        await _store.MergeOrUpdateAsync(new List<TrainingSample> { match }).ConfigureAwait(false);

        return new ReviewApplyResult(
            Found: true,
            Indexed: false,
            Deindexed: true,
            CorrectedSampleId: correctedSampleId);
    }
}
