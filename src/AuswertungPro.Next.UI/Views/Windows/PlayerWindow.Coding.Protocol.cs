using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- Coding PDF-Export ---

    private void CodingOfferPdfExport(ProtocolDocument doc)
    {
        if (_dependencies.ProtocolPdfExporter == null || _haltungRecord == null) return;

        var exported = CodingProtocolPdfExportServiceFactory.Create(_dependencies.ProtocolPdfExporter)
            .TryOfferPdfExport(_haltungRecord, doc, _dependencies.LastProjectPath);

        if (exported)
            ShowOverlay("PDF-Protokoll erstellt", TimeSpan.FromSeconds(4));
    }

    // --- Coding: Existierende Protokoll-Eintraege laden ---

    /// <summary>
    /// Laedt existierende Protokoll-Eintraege aus der Haltung (Import/DataGrid) in die Events-Liste.
    /// </summary>
    private void LoadExistingProtocolEntries()
    {
        if (_codingVm == null || _haltungRecord == null) return;

        var events = CodingProtocolEventMapper.BuildExistingEvents(_haltungRecord.Protocol);
        if (events.Count == 0) return;

        CodingProtocolEventCollectionAppender.Append(_codingVm.Events, events);
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        CodingPrimaryDamageSynchronizerFactory.Create().Sync(_haltungRecord, doc);
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        if (_haltungRecord == null || _dependencies.LegacyServiceProvider == null) return;

        var opened = CodingProtocolPreviewWorkflowServiceFactory.Create().TryShow(
            this,
            _haltungRecord,
            doc,
            _dependencies.LegacyServiceProvider,
            _videoPath,
            _dependencies.LastProjectPath,
            MarkProjectDirtyForCoding);
        if (!opened) return;

        // Nach Bearbeitung: Primaere Schaeden erneut synchronisieren.
        if (_haltungRecord.Protocol != null)
            SyncCodingToPrimaryDamages(_haltungRecord.Protocol);

        // PDF anbieten.
        CodingOfferPdfExport(_haltungRecord.Protocol ?? doc);
    }
}
