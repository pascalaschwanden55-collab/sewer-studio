using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingImportReferences()
    {
        if (!_codingSessionHost.HasViewModel)
            return;

        var eventCollection = _codingSessionHost.EventCollection;
        if (eventCollection is null)
            return;

        _lastCodingMatch = CodingProtocolMatchStateResetter.Reset(_codingProtocolMatchBuckets);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
            eventCollection,
            _codingImportEvents);
        LstImportEvents.ItemsSource = _codingImportEvents;
        CodingImportReferenceControls.SetCount(RunImportDefectCount, _codingImportEvents.Count);

        // CompleteSession soll nur neue KI-Events enthalten.
        CodingSessionEventResetter.ClearActiveSessionEvents(_codingSessionRuntimeOwner.Service);

        LstCodingEvents.ItemsSource = eventCollection;
        CodingImportReferenceControls.SetCount(RunCodingDefectCount, 0);
        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(eventCollection);
        _streckenTracker.Reset();
    }
}
