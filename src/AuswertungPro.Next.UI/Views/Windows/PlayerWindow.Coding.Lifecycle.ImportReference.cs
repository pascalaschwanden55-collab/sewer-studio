using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void InitializeCodingImportReferences()
    {
        if (_codingVm == null)
            return;

        _lastCodingMatch = null;
        _codingProtocolMatchBuckets.Clear();
        UpdateCodingProtocolMatchSummary(null);
        CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
            _codingVm.Events,
            _codingImportEvents);
        LstImportEvents.ItemsSource = _codingImportEvents;
        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();

        // CompleteSession soll nur neue KI-Events enthalten.
        CodingSessionEventResetter.ClearActiveSessionEvents(_codingSessionService);

        LstCodingEvents.ItemsSource = _codingVm.Events;
        RunCodingDefectCount.Text = "0";
        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(_codingVm.Events);
        _streckenTracker.Reset();
    }
}
