using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record SchachtProtocolSingleImportActions(
    Func<string, string, Task<SchachtProtocolParseResult?>> ReadProtocolAsync,
    ProjectOperationCheck ProjectIsStillOpen,
    object CollectionLock,
    Func<bool> SaveProject,
    Action<SchachtRecord> SetSelected,
    Action<SchachtRecord> ClearSelectedIfSame,
    Action<string> SetLastResult);

/// <summary>
/// Steuert den Import genau einer Schachtprotokoll-PDF einschliesslich
/// Zielentscheidung, Projektkopie und anschliessender Datensatzuebernahme.
/// </summary>
internal sealed class SchachtProtocolSingleImportController
{
    private const string DialogTitle = "Protokoll importieren";
    private readonly IDialogService _dialogs;
    private readonly ISchachtProtocolImportService _protocolImport;
    private readonly SchachtProtocolSingleImportActions _actions;

    internal SchachtProtocolSingleImportController(
        IDialogService dialogs,
        ISchachtProtocolImportService protocolImport,
        SchachtProtocolSingleImportActions actions)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _protocolImport = protocolImport ?? throw new ArgumentNullException(nameof(protocolImport));
        ArgumentNullException.ThrowIfNull(actions);
        Validate(actions);
        _actions = actions;
    }

    internal async Task ExecuteAsync(
        ProjectOperationContext projectContext,
        string projectFolder,
        string pdfPath)
    {
        var project = projectContext.Project;
        var result = await _actions.ReadProtocolAsync(pdfPath, DialogTitle);
        if (result is null
            || !_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                ProjectOperationImpact.None))
            return;

        if (!result.IstSchachtprotokoll)
        {
            var warning = string.IsNullOrWhiteSpace(result.Lesehinweis)
                ? "Das gewaehlte PDF ist kein Schachtprotokoll."
                : result.Lesehinweis;
            _dialogs.Warn(warning, DialogTitle);
            return;
        }

        if (string.IsNullOrWhiteSpace(result.Schachtnummer))
        {
            _dialogs.Warn(
                "Im Protokoll wurde keine Schachtnummer gefunden.",
                DialogTitle);
            return;
        }

        var targetResolution = ResolveTarget(project, result);
        if (targetResolution is null)
            return;
        var target = targetResolution.Target;

        SchachtProtocolDistributionResult distribution;
        try
        {
            _actions.SetLastResult(
                $"Schacht {result.Schachtnummer}: PDF wird ins Projekt kopiert ...");
            distribution = await Task.Run(() =>
                DistributePdf(
                    projectFolder,
                    result.Schachtnummer,
                    pdfPath));
        }
        catch (Exception ex)
        {
            _actions.SetLastResult("Protokoll konnte nicht kopiert werden.");
            var userMessage = UserError.DescribeAndReport(ex, "Schachtprotokoll kopieren");
            _dialogs.Warn(
                $"Das PDF konnte nicht ins Projekt kopiert werden:\n{userMessage}",
                DialogTitle);
            return;
        }

        var fileImpact = distribution.FileCreated
            ? ProjectOperationImpact.ProjectFilesWritten
            : ProjectOperationImpact.None;
        if (!_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                fileImpact))
            return;

        var targetRemoved = false;
        lock (_actions.CollectionLock)
        {
            if (targetResolution.RequiresProjectMembership
                && !project.SchaechteData.Contains(target))
            {
                targetRemoved = true;
            }
            else
            {
                _protocolImport.Apply(target, result, distribution.RelativePath);
                if (!targetResolution.RequiresProjectMembership
                    && !project.SchaechteData.Contains(target))
                {
                    project.SchaechteData.Add(target);
                }
            }
        }

        if (targetRemoved)
        {
            var removed =
                $"Protokoll nicht uebernommen: Schacht {result.Schachtnummer} wurde inzwischen entfernt.";
            _actions.SetLastResult(removed);
            _dialogs.Warn(
                removed + " Der geloeschte Datensatz wurde nicht wieder eingefuegt.",
                DialogTitle);
            return;
        }

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        var committedImpact = fileImpact | ProjectOperationImpact.ProjectDataChanged;
        if (!_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                committedImpact))
        {
            return;
        }

        _actions.SetSelected(target);
        if (!_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                committedImpact))
        {
            _actions.ClearSelectedIfSame(target);
            return;
        }

        var saved = ProjectSaveAttempt.Try(
            _actions.SaveProject,
            "Importiertes Schachtprotokoll speichern",
            out var saveError);
        if (!saved)
        {
            var notSaved =
                $"Protokoll uebernommen, aber nicht gespeichert: Schacht {result.Schachtnummer} " +
                $"({result.Schaeden.Count} Beobachtungen).";
            _actions.SetLastResult(notSaved);
            _dialogs.Warn(
                notSaved + "\n\nBitte das Projekt erneut speichern."
                + ProjectSaveAttempt.ErrorDetails(saveError),
                DialogTitle);
            return;
        }

        _actions.SetLastResult(
            $"Protokoll importiert: Schacht {result.Schachtnummer} " +
            $"({result.Schaeden.Count} Beobachtungen).");
    }

    private SchachtProtocolDistributionResult DistributePdf(
        string projectFolder,
        string shaftNumber,
        string sourcePath)
    {
        if (_protocolImport is ISchachtProtocolDistributionResultService detailedService)
        {
            return detailedService.DistributePdfWithResult(
                projectFolder,
                shaftNumber,
                sourcePath);
        }

        // Alte Implementierungen kennen nur die kompatible Pfad-Fassade. Ein
        // erfolgreicher Kopieraufruf wird vorsichtshalber als Dateiwirkung behandelt.
        return new SchachtProtocolDistributionResult(
            _protocolImport.DistributePdf(projectFolder, shaftNumber, sourcePath),
            FileCreated: true);
    }

    private TargetResolution? ResolveTarget(
        Project project,
        SchachtProtocolParseResult result)
    {
        var existing = _protocolImport.FindSchacht(
            project,
            result.Schachtnummer);
        if (existing is null)
            return new TargetResolution(
                new SchachtRecord(),
                RequiresProjectMembership: false);

        var choice = _dialogs.ConfirmCancel(
            $"Schacht {result.Schachtnummer} ist bereits vorhanden.\n\n" +
            "Ja = Ueberschreiben\nNein = Als neuen Schacht anlegen\nAbbrechen = Nichts tun",
            DialogTitle);

        return choice switch
        {
            DialogConfirm.Yes => new TargetResolution(
                existing,
                RequiresProjectMembership: true),
            DialogConfirm.No => new TargetResolution(
                new SchachtRecord(),
                RequiresProjectMembership: false),
            _ => null
        };
    }

    private sealed record TargetResolution(
        SchachtRecord Target,
        bool RequiresProjectMembership);

    private static void Validate(SchachtProtocolSingleImportActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions.ReadProtocolAsync);
        ArgumentNullException.ThrowIfNull(actions.ProjectIsStillOpen);
        ArgumentNullException.ThrowIfNull(actions.CollectionLock);
        ArgumentNullException.ThrowIfNull(actions.SaveProject);
        ArgumentNullException.ThrowIfNull(actions.SetSelected);
        ArgumentNullException.ThrowIfNull(actions.ClearSelectedIfSame);
        ArgumentNullException.ThrowIfNull(actions.SetLastResult);
    }
}
