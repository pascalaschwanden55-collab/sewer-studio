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
        if (_serviceProvider == null || _haltungRecord == null) return;

        var createPdf = CodingProtocolDialogServiceFactory.Create()
            .ConfirmPdfExport(doc.Current.Entries.Count);

        if (!createPdf) return;

        try
        {
            var plan = CodingProtocolPdfExportPlanner.Build(
                _haltungRecord,
                _serviceProvider.Settings.LastProjectPath,
                AppContext.BaseDirectory,
                PlayerClock.Now());

            var outputPath = CodingProtocolPdfSavePathDialogFactory.Create().Show(plan.DefaultFileName);
            if (outputPath == null) return;

            var project = PlayerShellProjectServiceFactory.Create().GetCurrentProject();
            var pdf = _serviceProvider.ProtocolPdfExporter.BuildHaltungsprotokollPdf(
                project!, _haltungRecord, doc, plan.ProjectRoot, plan.Options);
            CodingProtocolPdfFileServiceFactory.Create().SaveAndOpen(outputPath, pdf);

            ShowOverlay("PDF-Protokoll erstellt", TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            CodingProtocolDialogServiceFactory.Create().ShowPdfExportFailed(ex.Message);
        }
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

        foreach (var codingEvent in events)
            _codingVm.Events.Add(codingEvent);
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var primaryText = CodingPrimaryDamageTextBuilder.Build(doc);
        _haltungRecord.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
        _haltungRecord.ModifiedAtUtc = PlayerClock.UtcNow();
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        if (_haltungRecord == null || _serviceProvider == null) return;

        var showProtocol = CodingProtocolDialogServiceFactory.Create()
            .ConfirmProtocolPreview(doc.Current.Entries.Count);

        if (!showProtocol) return;

        var project = PlayerShellProjectServiceFactory.Create().GetCurrentProject();
        if (project == null) return;

        var projectFolder = CodingProjectFolderResolver.ResolveNullable(_serviceProvider.Settings.LastProjectPath);

        var dlg = new Views.ProtocolObservationsWindow(
            _haltungRecord, project, _serviceProvider, _videoPath, projectFolder,
            markDirty: () =>
            {
                MarkProjectDirtyForCoding();
            });
        dlg.Owner = this;
        dlg.ShowDialog();

        // Nach Bearbeitung: Primaere Schaeden erneut synchronisieren.
        if (_haltungRecord.Protocol != null)
            SyncCodingToPrimaryDamages(_haltungRecord.Protocol);

        // PDF anbieten.
        CodingOfferPdfExport(_haltungRecord.Protocol ?? doc);
    }
}
