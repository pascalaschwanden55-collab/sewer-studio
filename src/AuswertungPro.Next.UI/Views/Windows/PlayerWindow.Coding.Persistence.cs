using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingTrainingSamplePersistenceCoordinator? _codingTrainingSamples;

    private CodingTrainingSamplePersistenceCoordinator CodingTrainingSamples
        => _codingTrainingSamples ??= CodingTrainingSamplePersistenceCoordinator.CreateDefault(
            () => _codingSessionService,
            _dependencies.Settings);

    private async System.Threading.Tasks.Task PersistSingleEventAsTrainingSample(CodingEvent ev)
        => await CodingTrainingSamples.PersistSingleEventAsync(
            ev,
            CreateCodingTrainingSamplePersistenceRequest(_detectionConfirmationBuffer.FrameBytes));

    private void PersistCodingEventsAsTrainingSamples()
    {
        var events = _codingSessionHost.EventCollection;
        if (!_codingSessionHost.HasViewModel || events is null || events.Count == 0) return;

        CodingTrainingSamples
            .PersistEventsAsync(
                events,
                CreateCodingTrainingSamplePersistenceRequest(_detectionConfirmationBuffer.FrameBytes))
            .SafeFireAndForget("TrainingSave");
    }

    private CodingTrainingSamplePersistenceRequest CreateCodingTrainingSamplePersistenceRequest(byte[]? preferredFrameBytes)
        => CodingTrainingSamplePersistenceRequest.FromPlayerContext(
            _codingSessionHost.HaltungName ?? "unknown",
            _haltungRecord?.GetFieldValue("Datum_Jahr"),
            PlayerUserNameProvider.Current(),
            PlayerClock.UtcNow(),
            preferredFrameBytes,
            CaptureCurrentFrameAsync);
}
