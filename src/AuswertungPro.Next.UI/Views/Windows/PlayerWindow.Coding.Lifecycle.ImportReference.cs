using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingImportReferences()
    {
        var eventCollection = _codingSessionHost.EventCollection;
        CodingImportReferenceInitializationWorkflow.Execute(
            new CodingImportReferenceInitializationWorkflowRequest(
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                HasEventCollection: eventCollection is not null),
            new CodingImportReferenceInitializationWorkflowActions(
                ResetProtocolMatchState: _codingProtocolMatchState.Reset,
                UpdateProtocolMatchSummary: UpdateCodingProtocolMatchSummary,
                MoveExistingEventsToImportReference: () => CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
                    eventCollection!,
                    _codingImportReferenceEvents.Events),
                SetImportItemsSource: () => LstImportEvents.ItemsSource = _codingImportReferenceEvents.Events,
                SetImportCount: count => CodingImportReferenceControls.SetCount(RunImportDefectCount, count),
                ClearActiveSessionEvents: () => CodingSessionEventResetter.ClearActiveSessionEvents(_codingSessionRuntimeOwner.Service),
                SetCodingItemsSource: () => LstCodingEvents.ItemsSource = eventCollection,
                SetCodingCount: count => CodingImportReferenceControls.SetCount(RunCodingDefectCount, count),
                BuildBaselineSignature: () => CodingEventsSignatureBuilder.Build(eventCollection!),
                SetBaselineSignature: _codingBaselineSignatureState.Set,
                ResetStretchTracker: _streckenschadenTracker.Reset));
    }
}
