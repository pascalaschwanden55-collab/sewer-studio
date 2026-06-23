using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingApply_Click(object sender, RoutedEventArgs e)
        => ApplyCodingChanges(showOverlay: true);

    private bool ApplyCodingChanges(bool showOverlay)
    {
        if (_codingVm == null || _haltungRecord == null) return false;

        var doc = _haltungRecord.Protocol is null
            ? new ProtocolDocument { HaltungId = _haltungRecord.GetFieldValue("Haltungsname") }
            : AppProtocol.ProtocolRevisionCloner.CloneDocument(_haltungRecord.Protocol);
        doc.Current ??= new ProtocolRevision();
        doc.Current.Entries ??= new List<ProtocolEntry>();

        var eventEntryCount = _codingVm.Events.Count(
            ev => !string.IsNullOrWhiteSpace(ev.Entry.Code));

        var emptyGuard = CodingApplyEmptyProtocolGuard.Build(eventEntryCount, doc.Current.Entries);
        if (!CodingApplyDialogServiceFactory.Create().ConfirmEmptyProtocol(emptyGuard))
            return false;

        CodingProtocolRevisionUpdater.ApplyCodingEvents(doc.Current, _codingVm.Events);

        _haltungRecord.Protocol = doc;
        MarkProjectDirtyForCoding();

        SyncCodingToPrimaryDamages(doc);
        MarkProjectDirtyForCoding();

        PersistCodingEventsAsTrainingSamples();

        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(_codingVm.Events);

        SaveProjectAfterCoding();

        if (showOverlay)
        {
            var message = _codingVm.Events.Count == 0
                ? "Prim\u00e4re Sch\u00e4den geleert"
                : $"{_codingVm.Events.Count} Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen";
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
        if (!_isCodingMode || _codingVm is null)
            return false;

        var current = CodingEventsSignatureBuilder.Build(_codingVm.Events);
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
