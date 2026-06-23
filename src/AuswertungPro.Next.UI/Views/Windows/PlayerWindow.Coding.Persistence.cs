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
            CreateCodingTrainingSamplePersistenceRequest(_detectionPendingFrameBytes));

    private void PersistCodingEventsAsTrainingSamples()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0) return;

        CodingTrainingSamples
            .PersistEventsAsync(
                _codingVm.Events,
                CreateCodingTrainingSamplePersistenceRequest(_detectionPendingFrameBytes))
            .SafeFireAndForget("TrainingSave");
    }

    private CodingTrainingSamplePersistenceRequest CreateCodingTrainingSamplePersistenceRequest(byte[]? preferredFrameBytes)
        => CodingTrainingSamplePersistenceRequest.FromPlayerContext(
            _codingVm?.HaltungName ?? "unknown",
            _haltungRecord?.GetFieldValue("Datum_Jahr"),
            PlayerUserNameProvider.Current(),
            PlayerClock.UtcNow(),
            preferredFrameBytes,
            CaptureCurrentFrameAsync);
}
