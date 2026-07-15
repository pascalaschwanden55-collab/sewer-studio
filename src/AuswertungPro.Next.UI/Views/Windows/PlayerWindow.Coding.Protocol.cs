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
        CodingProtocolPdfExportCommandWorkflow.Execute(
            new CodingProtocolPdfExportCommandRequest(
                _protocolContext.ProtocolPdfExports is not null,
                _protocolContext.HasHaltungRecord,
                doc),
            new CodingProtocolPdfExportCommandActions(
                OfferPdfExport: () => CodingProtocolPdfExportDisplayWorkflow.Offer(
                    new CodingProtocolPdfExportDisplayRequestCore(
                        _protocolContext.HaltungRecord!,
                        doc,
                        _protocolContext.LastProjectPath,
                        _protocolContext.ProtocolPdfExports!)),
                ShowOverlay: ShowOverlay));
    }

    // --- Coding: Existierende Protokoll-Eintraege laden ---

    /// <summary>
    /// Laedt existierende Protokoll-Eintraege aus der Haltung (Import/DataGrid) in die Events-Liste.
    /// </summary>
    private void LoadExistingProtocolEntries()
    {
        CodingExistingProtocolEntriesWorkflow.Execute(
            new CodingExistingProtocolEntriesWorkflowRequest(
                _codingSessionHost.HasViewModel,
                _protocolContext.HaltungRecord,
                _codingSessionHost.EventCollection));
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        CodingPrimaryDamageSyncCommandWorkflow.Execute(
            new CodingPrimaryDamageSyncCommandRequest(_protocolContext.HasHaltungRecord),
            new CodingPrimaryDamageSyncCommandActions(
                SyncPrimaryDamages: () => CodingPrimaryDamageSyncWorkflow.Sync(
                    _protocolContext.HaltungRecord!,
                    doc)));
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                _protocolContext.HasHaltungRecord,
                _protocolContext.LegacyServiceProvider is not null,
                doc),
            new CodingProtocolPreviewCommandActions(
                ShowPreview: () => CodingProtocolPreviewDisplayWorkflow.TryShow(
                    this,
                    _protocolContext.HaltungRecord!,
                    doc,
                    _protocolContext.LegacyServiceProvider!,
                    _playbackContext.VideoPath,
                    _protocolContext.LastProjectPath,
                    _codingApplyController.MarkProjectDirty),
                GetCurrentProtocol: () => _protocolContext.HaltungRecord?.Protocol,
                SyncPrimaryDamages: SyncCodingToPrimaryDamages,
                OfferPdfExport: CodingOfferPdfExport));
    }
}
