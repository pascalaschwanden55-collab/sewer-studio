using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private CodingTrainingSamplePersistenceCoordinator CodingTrainingSamples
        => _codingTrainingSamplesOwner.Coordinator;

    private async System.Threading.Tasks.Task PersistSingleEventAsTrainingSample(CodingEvent ev)
        => await CodingTrainingSamples.PersistSingleEventAsync(
            ev,
            CreateCodingTrainingSamplePersistenceRequest(_liveDetectionController.PendingConfirmationFrameBytes));

    private void PersistCodingEventsAsTrainingSamples(IReadOnlyList<CodingEvent> events)
    {
        CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                _codingSessionHost.HasViewModel,
                events),
            new CodingTrainingBatchPersistenceWorkflowActions(
                PersistEvents: events => CodingTrainingSamples
                    .PersistEventsAsync(
                        events,
                        CreateCodingTrainingSamplePersistenceRequest(_liveDetectionController.PendingConfirmationFrameBytes))
                    .SafeFireAndForget("TrainingSave")));
    }

    private CodingTrainingSamplePersistenceRequest CreateCodingTrainingSamplePersistenceRequest(byte[]? preferredFrameBytes)
        => CodingTrainingSamplePersistenceRequest.FromPlayerContext(
            _codingSessionHost.HaltungName ?? "unknown",
            _protocolContext.HaltungRecord?.GetFieldValue("Datum_Jahr"),
            PlayerUserNameProvider.Current(),
            PlayerClock.UtcNow(),
            preferredFrameBytes,
            CaptureCurrentFrameAsync);
}
