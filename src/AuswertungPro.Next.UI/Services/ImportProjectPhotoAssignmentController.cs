using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportProjectPhotoAssignmentActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    Func<bool> SaveProject,
    Action<string> SetProgress,
    Action<string> AppendSummary,
    Action<string> AppendDetails);

/// <summary>
/// Steuert den UI-Ablauf zum Zuordnen externer Fotos zu einem Projekt.
/// </summary>
internal sealed class ImportProjectPhotoAssignmentController
{
    private readonly IDialogService _dialogs;
    private readonly IProjectPhotoAssignmentService _service;

    public ImportProjectPhotoAssignmentController(
        IDialogService dialogs,
        IProjectPhotoAssignmentService service)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public async Task ExecuteAsync(ImportProjectPhotoAssignmentActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info("Projekt bitte zuerst speichern.", "Fotos zuordnen");
            return;
        }

        var project = actions.GetProject();
        if (project.Data.Count == 0)
        {
            _dialogs.Info("Keine Haltungen im Projekt.", "Fotos zuordnen");
            return;
        }

        var sourceFolder = _dialogs.SelectFolder(
            "Quellordner mit den Fotos waehlen (z.B. der Foto-/Picture-Ordner des Exports)",
            null);
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return;

        actions.SetProgress("Fotos zuordnen: nach Haltung matchen, ins Projekt kopieren, verlinken...");
        var result = await Task.Run(() =>
            _service.AssignFromFolder(projectFolder, sourceFolder, project));
        actions.SetProgress(string.Empty);

        var saved = ProjectSaveAttempt.Try(
            actions.SaveProject,
            "Fotozuordnung speichern",
            out var saveError);

        var summary = "Fotos zugeordnet:"
            + $"\n  {result.HoldingsMatched} Haltungen mit Fotos"
            + $"\n  {result.PhotosAssigned} Fotos an Beobachtungen gehaengt"
            + $"\n  {result.PhotosCopied} ins Projekt kopiert"
            + $"\n  {result.UnmatchedFiles} nicht zuordenbar (z.B. GUID-benannt -> braucht DB-Import)";
        if (!saved)
        {
            summary += "\n\nAenderungen uebernommen, aber nicht gespeichert. Bitte erneut speichern."
                + ProjectSaveAttempt.ErrorDetails(saveError);
        }

        actions.AppendSummary("\n" + summary);
        if (result.Messages.Count > 0)
        {
            actions.AppendDetails(
                "\n\nFoto-Zuordnung:\n" + string.Join("\n", result.Messages.Take(50)));
        }

        if (saved)
            _dialogs.Info(summary, "Fotos zuordnen");
        else
            _dialogs.Warn(summary, "Fotos zuordnen");
    }
}
