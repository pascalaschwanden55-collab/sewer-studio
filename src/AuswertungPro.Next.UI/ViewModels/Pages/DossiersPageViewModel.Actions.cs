using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DossiersPageViewModel
{
    // ── Anlegen, aendern, loeschen ────────────────────────────────────────

    private async Task CreateDossierAsync()
    {
        if (!EnsureProject(out var root))
            return;

        var definition = new DossierDefinition();
        if (!DossierEditWindow.ShowFor(definition, isNew: true))
            return;

        definition.FolderName = DossierFolderPlanner.PlanFolderName(
            definition.Name,
            candidate => _document.Dossiers.Any(d =>
                string.Equals(d.FolderName, candidate, StringComparison.OrdinalIgnoreCase))
                || Directory.Exists(Path.Combine(
                    DossierFolderPlanner.ResolveRoot(root), candidate)));

        _document.Dossiers.Add(definition);

        if (!await SaveDocumentAsync(root))
        {
            _document.Dossiers.Remove(definition);
            return;
        }

        RebuildList();
        Selected = Dossiers.FirstOrDefault(d => d.Id == definition.Id);
        StatusMessage = $"Dossier „{definition.Name}\" angelegt.";
    }

    private async Task EditDossierAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        if (!DossierEditWindow.ShowFor(Selected.Definition, isNew: false))
            return;

        Selected.Definition.ModifiedAtUtc = DateTime.UtcNow;
        if (!await SaveDocumentAsync(root))
            return;

        RebuildList();
        StatusMessage = "Stammdaten gespeichert.";
    }

    private async Task EditAreaAsync()
    {
        if (!EnsureProject(out var root))
            return;

        if (!DossierAreaWindow.ShowFor(_document.Area))
            return;

        if (!await SaveDocumentAsync(root))
            return;

        AreaTitle = _document.Area.AreaTitle;
        RefreshDetail();
        StatusMessage = "Gebietsangaben gespeichert. Sie gelten für alle Dossiers.";
    }

    private async Task DeleteDossierAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var name = Selected.Name;
        if (!_dialogs.ConfirmWarn(
                $"Das Dossier „{name}\" aus der Liste entfernen?\n\n"
                + "Der Ordner mit Word-Datei und Beilagen bleibt erhalten und wird "
                + "NICHT gelöscht.",
                "Dossier entfernen"))
        {
            return;
        }

        var definition = Selected.Definition;
        _document.Dossiers.Remove(definition);

        if (!await SaveDocumentAsync(root))
        {
            _document.Dossiers.Add(definition);
            return;
        }

        Selected = null;
        RebuildList();
        StatusMessage = $"Dossier „{name}\" entfernt. Der Ordner blieb erhalten.";
    }

    private async Task EditHoldingsAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var chosen = DossierHoldingPickerWindow.ShowFor(
            _getProject(), Selected.Definition.HoldingIds);

        if (chosen is null)
            return;

        var previous = new List<Guid>(Selected.Definition.HoldingIds);
        Selected.Definition.HoldingIds = chosen;
        Selected.Definition.ModifiedAtUtc = DateTime.UtcNow;

        if (!await SaveDocumentAsync(root))
        {
            Selected.Definition.HoldingIds = previous;
            return;
        }

        RefreshDetail();
        StatusMessage = chosen.Count == 1
            ? "1 Leitung zugeordnet."
            : $"{chosen.Count} Leitungen zugeordnet.";
    }

    private async Task SetDossierStatusAsync(DossierStatus? status)
    {
        if (Selected is null || status is null || !EnsureProject(out var root))
            return;

        var previous = Selected.Definition.Status;
        Selected.Definition.Status = status.Value;

        if (!await SaveDocumentAsync(root))
        {
            Selected.Definition.Status = previous;
            return;
        }

        RefreshDetail();
        RebuildList();
        StatusMessage = "Stand: " + DescribeStatus(status.Value);
    }

    // ── Ausgabe ───────────────────────────────────────────────────────────

    private async Task CreateWordAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        IsBusy = true;
        try
        {
            var request = BuildRequest(root, Selected.Definition);
            var result = await _wordExport.ExportAsync(request);

            StatusMessage = result.Message;

            if (!result.Success)
            {
                _dialogs.Warn(result.Message, "Word-Datei");
                return;
            }

            if (Selected.Definition.Status == DossierStatus.Offen)
            {
                Selected.Definition.Status = DossierStatus.WordErzeugt;
                await SaveDocumentAsync(root);
                RebuildList();
            }

            _toasts.Success(result.Message);

            if (_dialogs.Confirm(
                    result.Message + "\n\nDatei jetzt in Word öffnen?", "Word-Datei"))
            {
                _shellOpen.TryOpen(result.FilePath!, out _);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CollectAttachmentsAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        IsBusy = true;
        try
        {
            var request = BuildRequest(root, Selected.Definition);
            var result = await _attachments.CollectAsync(request);

            var original = result.Attachments.Count(a =>
                a.Kind == DossierAttachmentKind.OriginalProtocol);
            var generated = result.Attachments.Count(a =>
                a.Kind == DossierAttachmentKind.GeneratedProtocol);

            var parts = new List<string>();
            if (original > 0)
                parts.Add($"{original}× Original-Protokoll");
            if (generated > 0)
                parts.Add($"{generated}× eigenes Protokoll");
            if (result.MissingCount > 0)
                parts.Add($"{result.MissingCount}× fehlt");

            StatusMessage = parts.Count == 0
                ? "Keine Leitungen zugeordnet — nichts zu sammeln."
                : "Beilagen: " + string.Join(", ", parts) + ".";

            if (result.Warnings.Count > 0)
            {
                _dialogs.Warn(
                    StatusMessage + "\n\n" + string.Join("\n", result.Warnings.Take(15)),
                    "Beilagen");
            }
            else
            {
                _toasts.Success(StatusMessage);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AssemblePdfAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        IsBusy = true;
        try
        {
            var folder = ResolveDossierFolder(root, Selected.Definition);
            var result = await _pdfAssembly.AssembleAsync(folder);

            StatusMessage = result.Message;

            if (!result.Success)
            {
                _dialogs.Warn(result.Message, "Gesamt-PDF");
                return;
            }

            _toasts.Success(result.Message);

            if (_dialogs.Confirm(result.Message + "\n\nPDF jetzt öffnen?", "Gesamt-PDF"))
                _shellOpen.TryOpen(result.FilePath!, out _);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenFolder()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var folder = ResolveDossierFolder(root, Selected.Definition);
        Directory.CreateDirectory(Path.Combine(
            folder, DossierFolderPlanner.AttachmentFolderName));

        _explorerReveal.TryReveal(folder, out _);
    }

    private async Task ResetTemplateAsync()
    {
        var path = DossierWordTemplateExportService.DefaultTemplatePath();

        if (File.Exists(path)
            && !_dialogs.ConfirmWarn(
                "Die vorhandene Word-Vorlage wird durch die Standardvorlage ersetzt.\n\n"
                + $"Datei: {path}\n\n"
                + "Eigene Änderungen an der Vorlage gehen dabei verloren. Fortfahren?",
                "Vorlage zurücksetzen"))
        {
            return;
        }

        try
        {
            await Task.Run(() => DossierWordTemplateBuilder.WriteTo(path));
            StatusMessage = "Standardvorlage wurde neu erstellt.";
            _toasts.Success(StatusMessage);

            if (_dialogs.Confirm(
                    "Vorlage neu erstellt.\n\nJetzt in Word öffnen, um sie anzupassen?",
                    "Word-Vorlage"))
            {
                _shellOpen.TryOpen(path, out _);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Die Vorlage konnte nicht erstellt werden: " + ex.Message;
            _dialogs.Error(StatusMessage, "Word-Vorlage");
        }
    }

    // ── Hilfen ────────────────────────────────────────────────────────────

    private DossierExportRequest BuildRequest(string root, DossierDefinition definition)
        => new(
            _getProject(),
            root,
            _document.Area,
            definition,
            DossierSnapshotBuilder.Build(definition, _getProject(), LoadCosts()),
            ResolveDossierFolder(root, definition));

    private static string ResolveDossierFolder(string root, DossierDefinition definition)
    {
        var folderName = string.IsNullOrWhiteSpace(definition.FolderName)
            ? DossierFolderPlanner.PlanFolderName(definition.Name, _ => false)
            : definition.FolderName;

        return Path.Combine(DossierFolderPlanner.ResolveRoot(root), folderName);
    }

    private bool EnsureProject(out string root)
    {
        root = _getProjectFolder() ?? "";

        if (string.IsNullOrWhiteSpace(root))
        {
            _dialogs.Warn(
                "Dossiers gehören zu einem Projekt. Bitte zuerst ein Projekt öffnen "
                + "oder speichern.",
                "Kein Projekt");
            return false;
        }

        if (!_loaded)
        {
            _dialogs.Warn(
                "Die Dossier-Datei konnte nicht gelesen werden. Es wird nichts "
                + "gespeichert, damit nichts überschrieben wird.\n\n" + StatusMessage,
                "Dossiers");
            return false;
        }

        return true;
    }

    private async Task<bool> SaveDocumentAsync(string root)
    {
        try
        {
            await _store.SaveAsync(root, _document);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Speichern fehlgeschlagen: " + ex.Message;
            _dialogs.Error(StatusMessage, "Dossiers");
            return false;
        }
    }
}
