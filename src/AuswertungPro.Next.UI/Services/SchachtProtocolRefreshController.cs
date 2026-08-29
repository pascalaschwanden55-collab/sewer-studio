using System.IO;
using AuswertungPro.Next.Application.Common;
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
    ForeignShaftNumber,
    TargetRemoved,
    UpdatedButNotSaved,
    Updated
}

internal sealed record SchachtProtocolRefreshActions(
    Func<string?> GetProjectFolder,
    Func<ProjectOperationContext> CaptureProject,
    Func<SchachtRecord, string, SchachtProtocolFileMatch?> LocateProtocolFile,
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
                "Von Hand geaenderte Felder bleiben erhalten; alle uebrigen werden ersetzt. Fortfahren?",
                DialogTitle))
        {
            return SchachtProtocolRefreshOutcome.Cancelled;
        }

        var match = _actions.LocateProtocolFile(selected, projectFolder);
        if (match is null)
        {
            _dialogs.Warn(
                "Die verknuepfte Protokoll-Datei wurde nicht gefunden.",
                DialogTitle);
            return SchachtProtocolRefreshOutcome.LinkedFileMissing;
        }

        var result = await _actions.ReadProtocolAsync(match.PdfPfad, DialogTitle);
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

        // Nur die ausdrueckliche Verknuepfung darf blind uebernommen werden. Eine im
        // Schachtordner selbst gefundene Datei muss zusaetzlich dieselbe Schachtnummer
        // tragen, sonst wird der Schacht nicht mit fremden Daten neu aufgebaut.
        if (match.Herkunft == SchachtProtocolFileOrigin.Schachtordner
            && !MatchesShaftNumber(selected, result.Schachtnummer)
            && !_dialogs.ConfirmWarn(
                $"Die verknuepfte Datei fehlt. Im Ordner dieses Schachts wurde stattdessen "
                + $"\"{Path.GetFileName(match.PdfPfad)}\" gefunden, sie gehoert laut Protokoll aber "
                + $"zu Schacht {result.Schachtnummer!.Trim()}. Trotzdem uebernehmen?",
                DialogTitle))
        {
            return SchachtProtocolRefreshOutcome.ForeignShaftNumber;
        }

        // Wurde die Datei erst im Schachtordner gefunden, ist die alte Verknuepfung
        // veraltet. Der neue Pfad wird dabei mitgespeichert.
        var pathForRecord = match.Herkunft == SchachtProtocolFileOrigin.Verknuepfung
            ? relativePath
            : ProjectPathResolver.MakeRelativeIfInsideProject(match.PdfPfad, projectFolder);

        // Zwischen Dateilesen, Rueckfragen und Uebernahme kann ein externer Aufrufer
        // das Projekt oder den Zielschacht austauschen. Direkt vor Apply deshalb
        // beides erneut pruefen; ein geloeschter Record darf nicht "offline" mutieren.
        if (!_actions.ProjectIsStillOpen(
                projectContext,
                DialogTitle,
                ProjectOperationImpact.None))
        {
            return SchachtProtocolRefreshOutcome.ProjectChanged;
        }

        if (!projectContext.Project.SchaechteData.Contains(selected))
        {
            const string removed =
                "Aktualisierung abgebrochen: Der ausgewaehlte Schacht wurde inzwischen entfernt.";
            _actions.SetLastResult(removed);
            _dialogs.Warn(
                removed + " Es wurden keine Protokolldaten uebernommen.",
                DialogTitle);
            return SchachtProtocolRefreshOutcome.TargetRemoved;
        }

        _actions.Apply(selected, result, pathForRecord);
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

        if (!ProjectSaveAttempt.Try(
                _actions.SaveProject,
                "Aktualisiertes Schachtprotokoll speichern",
                out var saveError))
        {
            var notSaved =
                $"Schacht {result.Schachtnummer} uebernommen, aber nicht gespeichert " +
                $"({result.Schaeden.Count} Beobachtungen).";
            _actions.SetLastResult(notSaved);
            _dialogs.Warn(
                notSaved + "\n\nBitte das Projekt erneut speichern."
                + ProjectSaveAttempt.ErrorDetails(saveError),
                DialogTitle);
            return SchachtProtocolRefreshOutcome.UpdatedButNotSaved;
        }

        _actions.SetLastResult(
            $"Schacht {result.Schachtnummer} aktualisiert ({result.Schaeden.Count} Beobachtungen).");
        return SchachtProtocolRefreshOutcome.Updated;
    }

    /// <summary>
    /// Vergleicht die im PDF gelesene Nummer mit der Nummer des ausgewaehlten Schachts.
    /// Ohne eigene Nummer am Record kann eine Ordnersuche gar nicht entstanden sein.
    /// </summary>
    private static bool MatchesShaftNumber(SchachtRecord selected, string? protocolNumber)
    {
        var recordNumber = selected.GetFieldValue("Schachtnummer")?.Trim();
        if (string.IsNullOrWhiteSpace(recordNumber))
            return false;

        return string.Equals(
            recordNumber,
            protocolNumber?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void Validate(SchachtProtocolRefreshActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions.GetProjectFolder);
        ArgumentNullException.ThrowIfNull(actions.CaptureProject);
        ArgumentNullException.ThrowIfNull(actions.LocateProtocolFile);
        ArgumentNullException.ThrowIfNull(actions.ReadProtocolAsync);
        ArgumentNullException.ThrowIfNull(actions.ProjectIsStillOpen);
        ArgumentNullException.ThrowIfNull(actions.Apply);
        ArgumentNullException.ThrowIfNull(actions.SaveProject);
        ArgumentNullException.ThrowIfNull(actions.SetLastResult);
    }
}
