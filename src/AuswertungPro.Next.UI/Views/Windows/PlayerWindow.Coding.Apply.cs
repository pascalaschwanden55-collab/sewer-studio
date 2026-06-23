using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
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

        // Schutz vor versehentlichem Leeren einer bestehenden Befundliste.
        if (eventEntryCount == 0)
        {
            var aktiveBefunde = doc.Current.Entries.Count(
                e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code));
            if (aktiveBefunde > 0)
            {
                var uebernehmen = DialogHost.Current.ConfirmWarn(
                    $"Die Befundliste ist leer.\n\n\"Übernehmen\" würde {aktiveBefunde} bestehende(n) Befund(e) dieser Haltung löschen und die primären Schäden leeren.\n\nWirklich eine leere Codierung übernehmen?",
                    "Leere Codierung übernehmen?");
                if (!uebernehmen)
                    return false;
            }
        }

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
                ? "Primäre Schäden geleert"
                : $"{_codingVm.Events.Count} Ereignisse in Primäre Schäden übernommen";
            ShowOverlay(message, TimeSpan.FromSeconds(4));
        }

        return true;
    }

    private bool ConfirmUnappliedCodingChangesOnClose()
    {
        if (!HasUnappliedCodingChanges())
            return true;

        SuspendCodingOverlayInput();
        DialogConfirm result;
        try
        {
            result = DialogHost.Current.ConfirmCancel(
                "Es gibt noch nicht übernommene Codierungen.\n\n" +
                "Ja = übernehmen\nNein = verwerfen\nAbbrechen = Fenster offen lassen",
                "Codier-Modus");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (result == DialogConfirm.Cancel)
            return false;

        if (result == DialogConfirm.Yes)
            return ApplyCodingChanges(showOverlay: false);

        return true;
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
        if (PlayerShellProjectServiceFactory.Create().MarkProjectDirty(_haltungRecord))
            return;

        if (_haltungRecord is not null)
            _haltungRecord.ModifiedAtUtc = PlayerClock.UtcNow();
    }

    private void SaveProjectAfterCoding()
    {
        // Nur speichern, wenn das Projekt bereits einen Pfad hat. Sonst wuerde TrySaveProject
        // mitten im Codieren oder beim Fensterschliessen einen Speichern-unter-Dialog oeffnen.
        PlayerShellProjectServiceFactory.Create().TrySaveProjectIfReady();
    }
}
