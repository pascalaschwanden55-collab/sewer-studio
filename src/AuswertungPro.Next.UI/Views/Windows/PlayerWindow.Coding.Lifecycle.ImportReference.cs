using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Behaviors;

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
                UpdateProtocolMatchSummary: _codingProtocolMatchController.UpdateSummary,
                MoveExistingEventsToImportReference: () => CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
                    eventCollection!,
                    _codingImportReferenceEvents.Events),
                SetImportItemsSource: () => CodingImportReferenceControls.SetItemsSource(
                    LstImportEvents,
                    _codingImportReferenceEvents.Events),
                SetImportCount: count => CodingImportReferenceControls.SetCount(RunImportDefectCount, count),
                ClearActiveSessionEvents: () => CodingSessionEventResetter.ClearActiveSessionEvents(_codingSessionRuntimeOwner.Service),
                SetCodingItemsSource: () => _codingSidePanelControllers.EventsList.SetItemsSource(eventCollection),
                SetCodingCount: count => CodingImportReferenceControls.SetCount(RunCodingDefectCount, count),
                BuildBaselineSignature: () => CodingEventsSignatureBuilder.Build(eventCollection!),
                SetBaselineSignature: _codingBaselineSignatureState.Set,
                ResetStretchTracker: _codingStreckenschadenTrackingController.Reset));

        WireCodingDragDrop();
    }

    /// <summary>
    /// Verdrahtet Drag&amp;Drop der Befund-Kacheln zwischen KI- und Import-Spalte:
    /// markiert die KI-Spalte und setzt fuer beide Listen denselben Drop-Callback.
    /// </summary>
    private void WireCodingDragDrop()
    {
        CodingEventDragDropBehavior.SetIsKiColumn(LstCodingEvents, true);
        CodingEventDragDropBehavior.SetIsKiColumn(LstImportEvents, false);
        CodingEventDragDropBehavior.SetDropHandler(LstCodingEvents, HandleAbgleichDrop);
        CodingEventDragDropBehavior.SetDropHandler(LstImportEvents, HandleAbgleichDrop);
    }

    /// <summary>
    /// Fuehrt einen Kachel-Drop im Abgleich-Panel aus. Die RICHTUNG bestimmt die Aktion:
    /// Import → KI (rechts → links) = NUR kopieren (Import-Referenz bleibt; KI bekommt einen offenen,
    /// noch UNBESTAETIGTEN Session-Befund via AddEvent). KI → Import (links → rechts) = verschieben und
    /// eingliedern (nach Meter sortiert) und dabei sauber aus der Session entfernen.
    /// </summary>
    private void HandleAbgleichDrop(CodingEvent ev, bool targetIsKi)
    {
        var session = _codingSessionRuntimeOwner.Service;
        var import = _codingImportReferenceEvents.Events;
        var result = _codingImportReferenceDropController.Execute(
            new CodingImportReferenceDropRequest(
                ev,
                targetIsKi,
                _codingSessionHost.EventCollection,
                import),
            new CodingImportReferenceDropActions(
                session is null ? null : (entry, overlay) => session.AddEvent(entry, overlay),
                session is null ? null : session.RemoveEvent));
        if (!result.Applied)
            return;

        RefreshCodingEventsList();
        CodingImportReferenceControls.SetCount(RunImportDefectCount, import.Count);
        _codingProtocolMatchController.RunMatch();
    }
}
