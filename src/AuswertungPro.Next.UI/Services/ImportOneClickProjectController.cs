using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportOneClickProjectActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    Func<Project, Project> DeepCopyProject,
    Action<Project> ReplaceProject,
    object CollectionLock,
    Func<bool> SaveProject,
    Action<string> SetProgress,
    Action<string> AppendSummary,
    Action<string> AppendDetails,
    Func<Project, string>? ComputeSignature = null);

/// <summary>Steuert den vollständigen Ein-Knopf-Import eines Kanalfernseh-Projekts.</summary>
internal sealed class ImportOneClickProjectController
{
    private readonly IDialogService _dialogs;
    private readonly Func<IOneClickProjectImportService> _createImporter;
    private readonly IOneClickImportReportWriter _reportWriter;

    public ImportOneClickProjectController(
        IDialogService dialogs,
        Func<IOneClickProjectImportService> createImporter,
        IOneClickImportReportWriter reportWriter)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _createImporter = createImporter ?? throw new ArgumentNullException(nameof(createImporter));
        _reportWriter = reportWriter ?? throw new ArgumentNullException(nameof(reportWriter));
    }

    public async Task ExecuteAsync(ImportOneClickProjectActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info(
                "Bitte zuerst ein Projekt anlegen/speichern.",
                "Import Kanalfernseh-Projekt");
            return;
        }

        var sourceFolder = _dialogs.SelectFolder(
            "Quellordner der Kanalfernsehdaten waehlen (WinCan-, IKAS- oder KINS-Projektordner)",
            null);
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return;

        // Wie beim manuellen Importlauf (ImportRunWorkflowController): der Import arbeitet
        // auf einer unabhaengigen Kopie. Erst nach einem erfolgreichen Lauf wird die
        // Live-Referenz getauscht — Fehler/Abbruch hinterlassen kein halb mutiertes Projekt.
        var liveProject = actions.GetProject();
        var projectContext = new ProjectOperationContext(liveProject, projectFolder);
        if (!TryComputeSignature(actions, liveProject, out var initialSignature))
            return;
        var targetProject = actions.DeepCopyProject(liveProject);

        actions.SetProgress(
            "Kanalfernseh-Projekt importieren: erkennen → archivieren → parsen → verteilen...");
        var context = new ImportRunContext(
            CancellationToken.None,
            null,
            new ImportRunLog(),
            collectionLock: actions.CollectionLock);

        OneClickProjectImportResult result;
        try
        {
            result = await Task.Run(() =>
                _createImporter().Import(sourceFolder, projectFolder, targetProject, context));
        }
        catch (Exception ex)
        {
            actions.SetProgress(string.Empty);
            var userMessage = UserError.DescribeAndReport(ex, "Kanalfernseh-Projekt importieren");
            _dialogs.Error(
                $"Import fehlgeschlagen — Projektdaten wurden nicht uebernommen:\n{userMessage}",
                "Import Kanalfernseh-Projekt");
            return;
        }
        actions.SetProgress(string.Empty);

        if (result.Format is OneClickProjectImportFormat.Unknown or OneClickProjectImportFormat.Ambiguous)
        {
            var hint = string.Join("\n", result.Messages.Take(6));
            _dialogs.Info(
                $"Format nicht eindeutig erkannt ({result.Format}).\n{hint}\n\n"
                + "Nutze ggf. die manuellen Import-Knoepfe (WinCan/XTF/PDF/IBAK/KINS).",
                "Import Kanalfernseh-Projekt");
            return;
        }

        // Hat der Nutzer waehrend des Laufs das aktive Projekt gewechselt, darf das
        // Ergebnis nicht in das neue Projekt gekippt werden (gleiche Regel wie im
        // manuellen Importlauf).
        if (!ActiveProjectGuard.IsCurrent(
                projectContext,
                actions.GetProject(),
                actions.GetProjectFolder()))
        {
            _dialogs.Error(
                "Waehrend des Imports wurde das aktive Projekt oder sein Speicherpfad gewechselt. " +
                "Das Importergebnis wurde aus Sicherheitsgruenden nicht uebernommen.",
                "Import Kanalfernseh-Projekt");
            return;
        }

        if (!TryComputeSignature(actions, liveProject, out var currentSignature))
            return;
        if (actions.ComputeSignature is not null
            && !string.Equals(initialSignature, currentSignature, StringComparison.Ordinal))
        {
            _dialogs.Error(
                "Das Projekt wurde waehrend des Imports bearbeitet. " +
                "Das Importergebnis wurde aus Sicherheitsgruenden nicht uebernommen; " +
                "die zwischenzeitlichen Aenderungen bleiben erhalten.",
                "Import Kanalfernseh-Projekt");
            return;
        }

        actions.ReplaceProject(targetProject);
        _reportWriter.TryWrite(projectFolder, result);

        var saved = ProjectSaveAttempt.Try(
            actions.SaveProject,
            "Kanalfernseh-Projekt nach Ein-Knopf-Import speichern",
            out var saveError);

        var summary = saved
            ? $"Import abgeschlossen ({result.Format}):"
            : $"Import uebernommen, aber Speichern fehlgeschlagen ({result.Format}):";
        summary += $"\n  {result.Found} Haltungen ({result.Created} neu, {result.Updated} aktualisiert)"
            + $"\n  {result.Errors} Fehler, {result.Conflicts} Feld-Konflikte"
            + "\n  Rohdaten archiviert, Filme/Fotos verteilt (Report in __IMPORT_REPORTS\\)";
        if (!saved)
        {
            summary += "\n  Hinweis: Die Projektdaten liegen nur im Arbeitsspeicher. " +
                       "Bitte das Projekt manuell speichern, sonst geht der Import beim Schliessen verloren."
                       + ProjectSaveAttempt.ErrorDetails(saveError);
        }
        actions.AppendSummary("\n" + summary);
        if (result.Messages.Count > 0)
        {
            actions.AppendDetails(
                "\n\nKanalfernseh-Import:\n" + string.Join("\n", result.Messages.Take(80)));
        }

        if (saved)
        {
            _dialogs.Info(summary, "Import Kanalfernseh-Projekt");
        }
        else
        {
            _dialogs.Error(summary, "Import Kanalfernseh-Projekt");
        }
    }

    private bool TryComputeSignature(
        ImportOneClickProjectActions actions,
        Project project,
        out string? signature)
    {
        signature = null;
        if (actions.ComputeSignature is null)
            return true;

        try
        {
            signature = actions.ComputeSignature(project);
            return true;
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(
                ex,
                "Projektinhalt fuer Kanalfernseh-Import pruefen");
            _dialogs.Error(
                "Der aktuelle Projektstand konnte nicht sicher geprueft werden. " +
                $"Das Importergebnis wurde nicht uebernommen.\n{userMessage}",
                "Import Kanalfernseh-Projekt");
            return false;
        }
    }
}
