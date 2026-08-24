using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models;
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

        // Zuerst Gemeinde und Parzelle: daraus fuellt der Kanton alles vor, was
        // er hergibt. Wer das nicht will, legt ohne Abfrage an.
        var idsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in _getProject().Data)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
            if (name.Length > 0)
                idsByName[name] = record.Id;
        }

        DossierParcelLookupChoice? abfrage;
        try
        {
            // Nur Schaechte, die das Hauptprojekt wirklich fuehrt.
            var schachtNummern = _getProject().SchaechteData
                .Select(s => (s.GetFieldValue("Schachtnummer") ?? string.Empty).Trim())
                .Where(n => n.Length > 0)
                .ToList();

            abfrage = DossierParcelLookupWindow.ShowFor(
                _parcels, _parcelLookup, _directory, idsByName, schachtNummern);
        }
        catch (Exception ex)
        {
            StatusMessage = "Die Abfrage konnte nicht geöffnet werden: " + ex.Message;
            _dialogs.Error(StatusMessage, "Neue Liegenschaft");
            return;
        }

        if (abfrage is null)
            return;

        var definition = abfrage.Dossier;

        foreach (var bezeichnung in abfrage.SelectedHoldingDesignations)
        {
            if (idsByName.TryGetValue(bezeichnung, out var id)
                && !definition.HoldingIds.Contains(id))
            {
                definition.HoldingIds.Add(id);
            }
        }

        definition.ShaftNumbers = abfrage.ShaftNumbers.ToList();

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

    /// <summary>
    /// Legt fuer die Parzellen des Projekts auf einmal Dossiers an. Die Regeln
    /// liegen in den Anwendungsfaellen; hier wird nur eingesammelt, das Fenster
    /// gezeigt und einmal gespeichert.
    /// </summary>
    private async Task CreateFromProjectAsync()
    {
        if (!EnsureProject(out var root))
            return;

        var project = _getProject();

        // Haltungsname -> Kennung. Ohne Namen laesst sich nichts zuordnen.
        var idsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in project.Data)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
            if (name.Length > 0)
                idsByName[name] = record.Id;
        }

        if (idsByName.Count == 0)
        {
            StatusMessage = "Das Projekt enthält keine Leitungen — es gibt nichts zu suchen.";
            return;
        }

        // Parzellen, fuer die es schon ein Dossier gibt, werden nicht erneut angeboten.
        // "439, 440" oder "762+756": das Feld ist Freitext. Jede einzelne Nummer
        // muss den Doppelten-Schutz ausloesen, nicht nur die ganze Zeichenkette.
        var mitDossier = _document.Dossiers
            .SelectMany(d => (d.ParcelNumbers ?? string.Empty)
                .Split(new[] { ',', ';', '+', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var erzeugte = DossierBatchWindow.ShowFor(
            _parcels,
            _batchProposal,
            idsByName.Keys.ToList(),
            idsByName,
            mitDossier);

        if (erzeugte.Count == 0)
        {
            StatusMessage = "Es wurden keine Dossiers erzeugt.";
            return;
        }

        foreach (var dossier in erzeugte)
        {
            dossier.FolderName = DossierFolderPlanner.PlanFolderName(
                dossier.Name,
                candidate => _document.Dossiers.Any(d =>
                    string.Equals(d.FolderName, candidate, StringComparison.OrdinalIgnoreCase))
                    || Directory.Exists(Path.Combine(
                        DossierFolderPlanner.ResolveRoot(root), candidate)));

            _document.Dossiers.Add(dossier);
        }

        // Alle auf einmal: ein Speichervorgang, nicht einer je Dossier.
        if (!await SaveDocumentAsync(root))
            return;

        await ReloadAsync();
        StatusMessage = erzeugte.Count == 1
            ? "1 Dossier erzeugt."
            : $"{erzeugte.Count} Dossiers erzeugt.";
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

    /// <summary>
    /// Oeffnet die ausgelieferte Word-Vorlage. Sie ist eine von Hand gestaltete
    /// Datei und wird nicht aus Code erzeugt — ein "Zuruecksetzen" gibt es
    /// deshalb nicht mehr; es haette die Vorlage nur zerstoert.
    /// </summary>
    private Task OpenTemplateAsync()
    {
        var path = DossierWordTemplateExportService.DefaultTemplatePath();

        if (!File.Exists(path))
        {
            StatusMessage = "Die Word-Vorlage fehlt: " + path;
            _dialogs.Error(StatusMessage, "Word-Vorlage");
            return Task.CompletedTask;
        }

        if (!_shellOpen.TryOpen(path, out var fehler))
        {
            StatusMessage = "Die Vorlage konnte nicht geöffnet werden: " + fehler;
            _dialogs.Error(StatusMessage, "Word-Vorlage");
            return Task.CompletedTask;
        }

        StatusMessage = "Word-Vorlage geöffnet.";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Zeigt das Dossier Seite fuer Seite und laesst die Felder dieser Seite
    /// direkt daneben ausfuellen. Uebernommen wird nur auf ausdruecklichen
    /// Wunsch — das Fenster arbeitet auf einer Kopie.
    /// </summary>
    private async Task PreviewAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var vorlage = DossierWordTemplateExportService.DefaultTemplatePath();
        if (!File.Exists(vorlage))
        {
            StatusMessage = "Die Word-Vorlage fehlt: " + vorlage;
            _dialogs.Error(StatusMessage, "Vorschau");
            return;
        }

        var definition = Selected.Definition;

        (DossierAreaSettings Area, DossierDefinition Dossier)? ergebnis;
        try
        {
            ergebnis = DossierPreviewWindow.ShowFor(
                BuildRequest(root, definition), vorlage, _planImages, _planAdjuster);
        }
        catch (Exception ex)
        {
            StatusMessage = "Die Vorschau konnte nicht geöffnet werden: " + ex.Message;
            _dialogs.Error(StatusMessage, "Vorschau");
            return;
        }

        if (ergebnis is null)
        {
            StatusMessage = "Vorschau geschlossen, nichts übernommen.";
            return;
        }

        var stelle = _document.Dossiers.FindIndex(d => d.Id == definition.Id);
        if (stelle < 0)
        {
            StatusMessage = "Das Dossier ist zwischenzeitlich verschwunden — nichts übernommen.";
            return;
        }

        // Die Vorschau hat auf Kopien gearbeitet. Zurueckgeschrieben wird an
        // genau die Stelle, von der die Kopie stammt — und der bisherige Stand
        // wird gemerkt: scheitert das Speichern, stuende sonst im Arbeitsspeicher
        // etwas anderes als in der Datei.
        var vorherigesGebiet = _document.Area;
        var vorherigesDossier = _document.Dossiers[stelle];

        _document.Area = ergebnis.Value.Area;
        _document.Dossiers[stelle] = ergebnis.Value.Dossier;

        if (!await SaveDocumentAsync(root))
        {
            _document.Area = vorherigesGebiet;
            _document.Dossiers[stelle] = vorherigesDossier;
            StatusMessage = "Nicht gespeichert — die Angaben bleiben wie vorher.";
            return;
        }

        await ReloadAsync();
        StatusMessage = "Angaben aus der Vorschau übernommen.";
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
