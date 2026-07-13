using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportProjectPortabilityActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    Func<bool> SaveProject,
    Action<string> SetProgress,
    Action<string> AppendSummary,
    Action<string> AppendDetails);

/// <summary>
/// Steuert den UI-Ablauf zum Umstellen eines Projekts auf portable Medienpfade.
/// </summary>
internal sealed class ImportProjectPortabilityController
{
    private readonly IDialogService _dialogs;
    private readonly IProjectPortabilityService _service;

    public ImportProjectPortabilityController(
        IDialogService dialogs,
        IProjectPortabilityService service)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public async Task ExecuteAsync(ImportProjectPortabilityActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info(
                "Projekt bitte zuerst speichern, dann kann es portabel gemacht werden.",
                "Projekt portabel machen");
            return;
        }

        var project = actions.GetProject();
        var count = project.Data.Count;
        if (count == 0)
        {
            _dialogs.Info("Keine Haltungen im Projekt.", "Projekt portabel machen");
            return;
        }

        actions.SetProgress("Projekt portabel machen: Medienpfade relativ verlinken, Fotos einsammeln...");
        var result = await Task.Run(() => _service.MakePortable(projectFolder, project));
        actions.SetProgress(string.Empty);

        _ = actions.SaveProject();

        var summary = $"Projekt portabel gemacht ({count} Haltungen):"
            + $"\n  {result.RelinkedPaths} Pfade relativ verlinkt"
            + $"\n  {result.FotosCopied} Fotos ins Projekt kopiert"
            + $"\n  {result.Unresolved} nicht aufloesbar";
        actions.AppendSummary("\n" + summary);
        if (result.Messages.Count > 0)
        {
            actions.AppendDetails(
                "\n\nPortabilitaet-Details:\n" + string.Join("\n", result.Messages.Take(50)));
        }

        _dialogs.Info(
            summary + "\n\nDer Projektordner kann jetzt 1:1 auf einen anderen PC kopiert werden.",
            "Projekt portabel machen");
    }
}
