using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal enum SchachtProtocolRefreshOutcome
{
    MissingSelection,
    MissingLinkedPdfPath,
    MissingProject,
    Cancelled,
    LinkedFileMissing,
    ReadFailed,
    ProjectChanged,
    InvalidProtocol,
    UpdatedButNotSaved,
    Updated
}

internal sealed record SchachtProtocolRefreshActions(
    Func<string?> GetProjectFolder,
    Func<ProjectOperationContext> CaptureProject,
    Func<string, string, string?> ResolveLinkedFile,
    Func<string, string, Task<SchachtProtocolParseResult?>> ReadProtocolAsync,
    ProjectOperationCheck ProjectIsStillOpen,
    Action<SchachtRecord, SchachtProtocolParseResult, string> Apply,
    Func<bool> SaveProject,
    Action<string> SetLastResult);

/// <summary>
/// Steuert das verhaltensgleiche Neueinlesen eines bereits verknuepften
/// Schachtprotokolls. Das ViewModel liefert nur noch seine Laufzeit-Aktionen.
/// </summary>
internal sealed class SchachtProtocolRefreshController
{
    private const string DialogTitle = "Aktualisieren";
    private const string InvalidProtocolFallback =
        "Das verknuepfte PDF ist kein lesbares Schachtprotokoll.";
    private readonly IDialogService _dialogs;
    private readonly SchachtProtocolRefreshActions _actions;

    internal SchachtProtocolRefreshController(
        IDialogService dialogs,
        SchachtProtocolRefreshActions actions)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        ArgumentNullException.ThrowIfNull(actions);
        Validate(actions);
        _actions = actions;
    }

    internal static bool CanExecute(SchachtRecord? selected)
        => selected is not null
           && !string.IsNullOrWhiteSpace(selected.GetFieldValue("PDF_Path"));

    internal async Task<SchachtProtocolRefreshOutcome> ExecuteAsync(SchachtRecord? selected)
    {
        if (selected is null)
            return SchachtProtocolRefreshOutcome.MissingSelection;

        var relativePath = selected.GetFieldValue("PDF_Path");
        if (string.IsNullOrWhiteSpace(relativePath))
            return SchachtProtocolRefreshOutcome.MissingLinkedPdfPath;

        var projectFolder = _actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info("Kein Projekt geoeffnet.", DialogTitle);
            return SchachtProtocolRefreshOutcome.MissingProject;
        }

        var projectContext = _actions.CaptureProject();

        if (!_dialogs.ConfirmWarn(
                "Der Schacht wird komplett aus dem Protokoll neu aufgebaut. " +
                "Von Hand erfasste Werte gehen dabei verloren. Fortfahren?",
                DialogTitle))
        {
            return SchachtProtocolRefreshOutcome.Cancelled;
        }

        var absolutePath = _actions.ResolveLinkedFile(relativePath, projectFolder);
        if (absolutePath is null)
        {
            _dialogs.Warn(
                "Die verknuepfte Protokoll-Datei wurde nicht gefunden.",
                DialogTitle);
            return SchachtProtocolRefreshOutcome.LinkedFileMissing;
        }

        var result = await _actions.ReadProtocolAsync(absolutePath, DialogTitle);
        if (result is null)
            return SchachtProtocolRefreshOutcome.ReadFailed;

        if (!_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                ProjectOperationImpact.None))
            return SchachtProtocolRefreshOutcome.ProjectChanged;

        if (!result.IstSchachtprotokoll || string.IsNullOrWhiteSpace(result.Schachtnummer))
        {
            var warning = string.IsNullOrWhiteSpace(result.Lesehinweis)
                ? InvalidProtocolFallback
                : result.Lesehinweis;
            _dialogs.Warn(warning, DialogTitle);
            return SchachtProtocolRefreshOutcome.InvalidProtocol;
        }

        _actions.Apply(selected, result, relativePath);
        var project = projectContext.Project;
        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        if (!_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                ProjectOperationImpact.ProjectDataChanged))
        {
            return SchachtProtocolRefreshOutcome.UpdatedButNotSaved;
        }

        _ = _actions.SaveProject();
        _actions.SetLastResult(
            $"Schacht {result.Schachtnummer} aktualisiert ({result.Schaeden.Count} Beobachtungen).");
        return SchachtProtocolRefreshOutcome.Updated;
    }

    private static void Validate(SchachtProtocolRefreshActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions.GetProjectFolder);
        ArgumentNullException.ThrowIfNull(actions.CaptureProject);
        ArgumentNullException.ThrowIfNull(actions.ResolveLinkedFile);
        ArgumentNullException.ThrowIfNull(actions.ReadProtocolAsync);
        ArgumentNullException.ThrowIfNull(actions.ProjectIsStillOpen);
        ArgumentNullException.ThrowIfNull(actions.Apply);
        ArgumentNullException.ThrowIfNull(actions.SaveProject);
        ArgumentNullException.ThrowIfNull(actions.SetLastResult);
    }
}
