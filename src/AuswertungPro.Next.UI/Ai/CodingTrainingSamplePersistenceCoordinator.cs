using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

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
        ITrainingSampleStore? trainingSamples = null)
        => new(
            new CodingTrainingFrameStore(),
            new CodingTrainingSamplePersister(sessionProvider, trainingSamples),
            new CodingTrainingSampleEvalProtector(settings),
            message => BestEffort.ReportWarning(message));

    public async Task PersistSingleEventAsync(CodingEvent codingEvent, CodingTrainingSamplePersistenceRequest request)
    {
        if (!CanPersist(codingEvent))
            return;

        try
        {
            var framePath = CodingTrainingSampleFactory.PrimaryFramePath(codingEvent);
            string? snapshotError = null;
            if (string.IsNullOrWhiteSpace(framePath))
            {
                var savedGoldFrame = await _frameStore.SaveGoldFrameAsync(
                    codingEvent,
                    request.PreferredFrameBytes,
                    request.CaptureFrameAsync);
                framePath = savedGoldFrame.Path;
                snapshotError = savedGoldFrame.Error;
            }

            var evidenceFrame = _frameStore.SaveEvidenceFrame(codingEvent, framePath);
            if (evidenceFrame.Error != null)
                _log($"[Training] Beweisbild nicht gespeichert: {evidenceFrame.Error}");

            var sample = CreateSample(codingEvent, request, framePath, evidenceFrame.Path, snapshotError);
            if (IsProtected(sample))
                return;

            await _persister.SaveAndIndexAsync(sample);
        }
        catch (Exception ex)
        {
            _log($"[Training] Einzelspeicherung Fehler: {ex.Message}");
        }
    }

    public async Task PersistEventsAsync(IEnumerable<CodingEvent> codingEvents, CodingTrainingSamplePersistenceRequest request)
    {
        try
        {
            var samples = codingEvents
                .Where(CanPersist)
                .Select(ev => CreateSample(
                    ev,
                    request,
                    CodingTrainingSampleFactory.PrimaryFramePath(ev),
                    evidenceFramePath: null,
                    snapshotError: null))
                .Where(sample => !IsProtected(sample))
                .ToList();

            if (samples.Count > 0)
                await _persister.SaveAndIndexAsync(samples);
        }
        catch (Exception ex)
        {
            _log($"[Training] Fehler: {ex.Message}");
        }
    }

    private TrainingSample CreateSample(
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
        => !string.IsNullOrWhiteSpace(codingEvent.Entry.Code);
}
