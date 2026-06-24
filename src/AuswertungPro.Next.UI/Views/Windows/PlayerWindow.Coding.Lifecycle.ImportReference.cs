using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingImportReferences()
    {
        if (_codingVm == null)
            return;

        _lastCodingMatch = CodingProtocolMatchStateResetter.Reset(_codingProtocolMatchBuckets);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
            _codingVm.Events,
            _codingImportEvents);
        LstImportEvents.ItemsSource = _codingImportEvents;
        CodingImportReferenceControls.SetCount(RunImportDefectCount, _codingImportEvents.Count);

        // CompleteSession soll nur neue KI-Events enthalten.
        CodingSessionEventResetter.ClearActiveSessionEvents(_codingSessionService);

        LstCodingEvents.ItemsSource = _codingVm.Events;
        CodingImportReferenceControls.SetCount(RunCodingDefectCount, 0);
        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(_codingVm.Events);
        _streckenTracker.Reset();
    }
}
