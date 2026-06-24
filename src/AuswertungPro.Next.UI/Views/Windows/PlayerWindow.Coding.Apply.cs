using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingApply_Click(object sender, RoutedEventArgs e)
        => ApplyCodingChanges(showOverlay: true);

    private bool ApplyCodingChanges(bool showOverlay)
    {
        if (!_codingSessionHost.HasViewModel || _haltungRecord == null) return false;

        var events = _codingSessionHost.EventCollection;
        if (events is null) return false;

        var update = CodingApplyProtocolUpdateBuilder.Create(_haltungRecord, events);
        var emptyGuard = CodingApplyEmptyProtocolGuard.Build(update.EventEntryCount, update.CurrentRevision.Entries);
        if (!CodingApplyDialogServiceFactory.Create().ConfirmEmptyProtocol(emptyGuard))
            return false;

        CodingProtocolRevisionUpdater.ApplyCodingEvents(update.CurrentRevision, update.Events);

        _haltungRecord.Protocol = update.Document;
        MarkProjectDirtyForCoding();

        SyncCodingToPrimaryDamages(update.Document);
        MarkProjectDirtyForCoding();

        PersistCodingEventsAsTrainingSamples();

        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(events);

        SaveProjectAfterCoding();

        if (showOverlay)
        {
            var message = events.Count == 0
                ? "Prim\u00e4re Sch\u00e4den geleert"
                : $"{events.Count} Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen";
            ShowOverlay(message, TimeSpan.FromSeconds(4));
        }

        return true;
    }

    private bool ConfirmUnappliedCodingChangesOnClose()
    {
        if (!HasUnappliedCodingChanges())
            return true;

        SuspendCodingOverlayInput();
        bool shouldClose;
        try
        {
            shouldClose = CodingApplyDialogServiceFactory.Create()
                .ConfirmUnappliedChangesOnClose(() => ApplyCodingChanges(showOverlay: false));
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        return shouldClose;
    }

    private bool HasUnappliedCodingChanges()
    {
        if (!_isCodingMode || !_codingSessionHost.HasViewModel)
            return false;

        var current = CodingEventsSignatureBuilder.Build(_codingSessionHost.Events);
        return !string.Equals(current, _codingBaselineSignature, StringComparison.Ordinal);
    }

    private void MarkProjectDirtyForCoding()
    {
        CodingProjectPersistenceServiceFactory.Create().MarkProjectDirty(_haltungRecord);
    }

    private void SaveProjectAfterCoding()
    {
        // Nur speichern, wenn das Projekt bereits einen Pfad hat. Sonst wuerde TrySaveProject
        // mitten im Codieren oder beim Fensterschliessen einen Speichern-unter-Dialog oeffnen.
        CodingProjectPersistenceServiceFactory.Create().TrySaveProjectIfReady();
    }
}
