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
                UpdateProtocolMatchSummary: UpdateCodingProtocolMatchSummary,
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
                ResetStretchTracker: _streckenschadenTracker.Reset));

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
    /// Fuehrt einen Kachel-Drop im Abgleich-Panel aus. Ziel = KI-Spalte -> echter, noch
    /// UNBESTAETIGTER Session-Befund (via AddEvent; EventAdded fuellt die VM-Liste). Ziel =
    /// Import -> reine UI-Referenz. Verschieben aus der KI entfernt zusaetzlich sauber aus der
    /// Session; Kopieren (Strg) dupliziert mit neuen IDs.
    /// </summary>
    private void HandleAbgleichDrop(CodingEvent ev, bool targetIsKi, bool isCopy)
    {
        var ki = _codingSessionHost.EventCollection;
        var import = _codingImportReferenceEvents.Events;
        if (ki is null)
            return;

        if (targetIsKi)
        {
            // In die KI-Spalte: als offenen Session-Befund anlegen (unabhaengiger Klon mit neuen IDs).
            var clone = CodingEventColumnTransfer.CloneWithNewIds(ev);
            _codingSessionRuntimeOwner.Service?.AddEvent(clone.Entry, clone.Overlay);
            if (!isCopy)
                import.Remove(ev); // Verschieben: aus der Import-Spalte entfernen
        }
        else
        {
            // In die Import-Spalte (reine UI-Referenz).
            if (isCopy)
            {
                CodingEventColumnTransfer.Copy(ev, import);
            }
            else
            {
                CodingEventColumnTransfer.Move(ev, ki, import);
                _codingSessionRuntimeOwner.Service?.RemoveEvent(ev.EventId); // aus der Session raus
            }
        }

        RefreshCodingEventsList();
        CodingImportReferenceControls.SetCount(RunImportDefectCount, import.Count);
        RunCodingProtocolMatch();
    }
}
