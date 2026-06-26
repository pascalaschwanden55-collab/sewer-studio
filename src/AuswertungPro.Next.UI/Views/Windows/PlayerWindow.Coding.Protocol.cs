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
                _dependencies.ProtocolPdfExporter is not null,
                _haltungRecord is not null,
                doc),
            new CodingProtocolPdfExportCommandActions(
                OfferPdfExport: () => CodingProtocolPdfExportServiceFactory
                    .Create(_dependencies.ProtocolPdfExporter!)
                    .TryOfferPdfExport(_haltungRecord!, doc, _dependencies.LastProjectPath),
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
                _haltungRecord,
                _codingSessionHost.EventCollection));
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        CodingPrimaryDamageSyncCommandWorkflow.Execute(
            new CodingPrimaryDamageSyncCommandRequest(_haltungRecord is not null),
            new CodingPrimaryDamageSyncCommandActions(
                SyncPrimaryDamages: () => CodingPrimaryDamageSyncWorkflow.Sync(
                    _haltungRecord!,
                    doc,
                    new CodingPrimaryDamageSyncWorkflowActions(
                        CreateSynchronizer: CodingPrimaryDamageSynchronizerFactory.Create))));
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        CodingProtocolPreviewCommandWorkflow.Execute(
            new CodingProtocolPreviewCommandRequest(
                _haltungRecord is not null,
                _dependencies.LegacyServiceProvider is not null,
                doc),
            new CodingProtocolPreviewCommandActions(
                ShowPreview: () => CodingProtocolPreviewWorkflowServiceFactory.Create().TryShow(
                    this,
                    _haltungRecord!,
                    doc,
                    _dependencies.LegacyServiceProvider!,
                    _videoPath,
                    _dependencies.LastProjectPath,
                    MarkProjectDirtyForCoding),
                GetCurrentProtocol: () => _haltungRecord?.Protocol,
                SyncPrimaryDamages: SyncCodingToPrimaryDamages,
                OfferPdfExport: CodingOfferPdfExport));
    }
}
