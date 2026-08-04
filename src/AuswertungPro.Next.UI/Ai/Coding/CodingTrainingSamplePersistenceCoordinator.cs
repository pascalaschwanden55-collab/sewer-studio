using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingTrainingSamplePersistenceRequest(
    string CaseId,
    DateTime? InspectionDate,
    string? ConfirmedByUser,
    DateTime? ConfirmedAtUtc,
    byte[]? PreferredFrameBytes,
    Func<Task<byte[]?>> CaptureFrameAsync)
{
    public static CodingTrainingSamplePersistenceRequest FromPlayerContext(
        string caseId,
        string? inspectionDateText,
        string? confirmedByUser,
        DateTime? confirmedAtUtc,
        byte[]? preferredFrameBytes,
        Func<Task<byte[]?>> captureFrameAsync)
        => new(
            caseId,
            TrainingSampleEligibility.TryParseInspectionDate(inspectionDateText),
            confirmedByUser,
            confirmedAtUtc,
            preferredFrameBytes,
            captureFrameAsync);
}

/// <summary>
/// Ergebnis einer Einzelspeicherung: Bei einem Fehler ist <see cref="Success"/> false und
/// <see cref="Error"/> enthaelt den Grund. Fachlich uebersprungene Samples (Eval-Schutz,
/// kein Code) gelten weiterhin als Erfolg — sie werden intern geloggt, sind aber kein
/// Speicherfehler, fuer den das UI eine Retry anbieten muesste.
/// </summary>
public sealed record CodingTrainingSamplePersistenceResult(bool Success, string? Error)
{
    public static CodingTrainingSamplePersistenceResult Ok { get; } = new(true, null);

    public static CodingTrainingSamplePersistenceResult Failed(string? error)
        => new(false, string.IsNullOrWhiteSpace(error) ? "Unbekannter Fehler" : error);
}

public sealed class CodingTrainingSamplePersistenceCoordinator
{
    private readonly CodingTrainingFrameStore _frameStore;
    private readonly CodingTrainingSamplePersister _persister;
    private readonly CodingTrainingSampleEvalProtector _evalProtector;
    private readonly Action<string> _log;

    public CodingTrainingSamplePersistenceCoordinator(
        CodingTrainingFrameStore frameStore,
        CodingTrainingSamplePersister persister,
        CodingTrainingSampleEvalProtector evalProtector,
        Action<string>? log = null)
    {
        _frameStore = frameStore ?? throw new ArgumentNullException(nameof(frameStore));
        _persister = persister ?? throw new ArgumentNullException(nameof(persister));
        _evalProtector = evalProtector ?? throw new ArgumentNullException(nameof(evalProtector));
        _log = log ?? (_ => { });
    }

    public static CodingTrainingSamplePersistenceCoordinator CreateDefault(
        Func<ICodingSessionService?> sessionProvider,
        AppSettings? settings,
        ITrainingSampleStore? trainingSamples = null,
        ITrainingFrameStore? trainingFrames = null)
        => new(
            new CodingTrainingFrameStore(
                trainingFrames ?? new TrainingFrameFileStore()),
            new CodingTrainingSamplePersister(sessionProvider, trainingSamples),
            new CodingTrainingSampleEvalProtector(settings),
            message => BestEffort.ReportWarning(message));

    public async Task<CodingTrainingSamplePersistenceResult> PersistSingleEventAsync(
        CodingEvent codingEvent,
        CodingTrainingSamplePersistenceRequest request)
    {
        if (!CanPersist(codingEvent))
            return CodingTrainingSamplePersistenceResult.Ok;

        try
        {
            var sample = await PrepareSampleAsync(
                    codingEvent,
                    request,
                    allowFrameCapture: true,
                    saveEvidenceFrame: true)
                .ConfigureAwait(false);
            if (sample is not null)
                await _persister.SaveAndIndexAsync(sample).ConfigureAwait(false);
            return CodingTrainingSamplePersistenceResult.Ok;
        }
        catch (Exception ex)
        {
            _log($"[Training] Einzelspeicherung Fehler: {ex.Message}");
            return CodingTrainingSamplePersistenceResult.Failed(ex.Message);
        }
    }

    public async Task PersistEventsAsync(
        IEnumerable<CodingEvent> codingEvents,
        CodingTrainingSamplePersistenceRequest request)
    {
        _ = await PersistEventsWithResultAsync(codingEvents, request).ConfigureAwait(false);
    }

    public async Task<CodingTrainingSamplePersistenceResult> PersistEventsWithResultAsync(
        IEnumerable<CodingEvent> codingEvents,
        CodingTrainingSamplePersistenceRequest request)
    {
        try
        {
            var samples = new List<TrainingSample>();
            foreach (var codingEvent in codingEvents.Where(CanPersist))
            {
                var sample = await PrepareSampleAsync(
                        codingEvent,
                        request,
                        allowFrameCapture: false,
                        saveEvidenceFrame: false)
                    .ConfigureAwait(false);
                if (sample is not null)
                    samples.Add(sample);
            }

            if (samples.Count > 0)
                await _persister.SaveAndIndexAsync(samples).ConfigureAwait(false);
            return CodingTrainingSamplePersistenceResult.Ok;
        }
        catch (Exception ex)
        {
            _log($"[Training] Fehler: {ex.Message}");
            return CodingTrainingSamplePersistenceResult.Failed(ex.Message);
        }
    }

    private async Task<TrainingSample?> PrepareSampleAsync(
        CodingEvent codingEvent,
        CodingTrainingSamplePersistenceRequest request,
        bool allowFrameCapture,
        bool saveEvidenceFrame)
    {
        var sourceFramePath = CodingTrainingSampleFactory.PrimaryFramePath(codingEvent);
        var sourceSample = CreateSample(
            codingEvent,
            request,
            sourceFramePath,
            evidenceFramePath: null,
            snapshotError: null);
        if (IsProtected(sourceSample))
            return null;

        var framePath = sourceFramePath;
        string? snapshotError = null;
        if (ManualGoldTrainingPolicy.IsManuallyConfirmed(sourceSample))
        {
            byte[]? frameBytes = null;
            if (string.IsNullOrWhiteSpace(sourceFramePath) && allowFrameCapture)
            {
                frameBytes = request.PreferredFrameBytes;
                if (frameBytes is null || frameBytes.Length == 0)
                    frameBytes = await request.CaptureFrameAsync().ConfigureAwait(false);
                if (_evalProtector.IsProtected(frameBytes, request.CaseId))
                {
                    _log(
                        $"[Training] Eval-Schutz: {request.CaseId}/{codingEvent.Entry.Code} " +
                        "NICHT als Gold gespeichert.");
                    return null;
                }
            }

            var savedGoldFrame = await _frameStore
                .SaveGoldFrameAsync(
                    codingEvent,
                    sourceFramePath,
                    frameBytes,
                    static () => Task.FromResult<byte[]?>(null))
                .ConfigureAwait(false);
            framePath = savedGoldFrame.Path;
            snapshotError = savedGoldFrame.Error;
            if (snapshotError is not null)
                _log($"[Training] Goldbild nicht gespeichert: {snapshotError}");
        }

        var sample = CreateSample(
            codingEvent,
            request,
            framePath,
            evidenceFramePath: null,
            snapshotError);
        if (IsProtected(sample))
            return null;

        if (ManualGoldTrainingPolicy.IsManuallyConfirmed(sample)
            && !string.IsNullOrWhiteSpace(sample.FramePath))
            sample.KbIndexState = KbIndexState.Pending;

        if (saveEvidenceFrame)
        {
            var evidenceFrame = _frameStore.SaveEvidenceFrame(codingEvent, framePath);
            sample.EvidenceFramePath = evidenceFrame.Path;
            if (evidenceFrame.Error is not null)
                _log($"[Training] Beweisbild nicht gespeichert: {evidenceFrame.Error}");
        }

        return sample;
    }

    private static TrainingSample CreateSample(
        CodingEvent codingEvent,
        CodingTrainingSamplePersistenceRequest request,
        string? framePath,
        string? evidenceFramePath,
        string? snapshotError)
        => CodingTrainingSampleFactory.Create(
            codingEvent,
            request.CaseId,
            framePath,
            request.InspectionDate,
            request.ConfirmedByUser,
            request.ConfirmedAtUtc,
            evidenceFramePath,
            snapshotError);

    private bool IsProtected(TrainingSample sample)
    {
        if (!_evalProtector.IsProtected(sample))
            return false;

        _log($"[Training] Eval-Schutz: {sample.CaseId}/{sample.Code} NICHT als Gold gespeichert.");
        return true;
    }

    private static bool CanPersist(CodingEvent codingEvent)
        => codingEvent.Entry.Training?.SkipAutomaticPersistence != true
           && !string.IsNullOrWhiteSpace(codingEvent.Entry.Code);
}
