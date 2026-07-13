using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportProtocolRegenerationActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    Func<bool> SaveProject,
    Action<string> SetProgress,
    Action<string> AppendSummary,
    Action<string> AppendDetails,
    Action<string> SetStatus);

/// <summary>
/// Steuert den UI-Ablauf zum Neuerzeugen der programmeigenen Haltungsprotokolle.
/// </summary>
internal sealed class ImportProtocolRegenerationController
{
    private readonly IDialogService _dialogs;
    private readonly IProtocolRegenerationService _service;
    private readonly ICodeCatalogProvider _codeCatalog;

    public ImportProtocolRegenerationController(
        IDialogService dialogs,
        IProtocolRegenerationService service,
        ICodeCatalogProvider codeCatalog)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _codeCatalog = codeCatalog ?? throw new ArgumentNullException(nameof(codeCatalog));
    }

    public async Task ExecuteAsync(ImportProtocolRegenerationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info(
                "Projekt bitte zuerst speichern, dann koennen die eigenen Protokolle erzeugt werden.",
                "Protokoll neu generieren");
            return;
        }

        var project = actions.GetProject();
        var count = project.Data.Count;
        if (count == 0)
        {
            _dialogs.Info("Keine Haltungen im Projekt.", "Protokoll neu generieren");
            return;
        }

        actions.SetProgress("Eigene Protokolle (_E, mit Fotos) werden fuer die Verteilung erzeugt...");
        var result = await Task.Run(() =>
            _service.RegenerateAll(project, projectFolder, _codeCatalog));
        actions.SetProgress(string.Empty);

        _ = actions.SaveProject();

        var summary = $"Eigene Protokolle neu generiert ({count} Haltungen):"
            + $"\n  {result.Generated} Protokolle erzeugt (_E, in die Verteilung)"
            + $"\n  {result.Errors} Fehler";
        actions.AppendSummary("\n" + summary);
        if (result.Messages.Count > 0)
        {
            actions.AppendDetails(
                "\n\nProtokoll-Details:\n" + string.Join("\n", result.Messages.Take(50)));
        }

        actions.SetStatus("Eigene Protokolle neu generiert");
        _dialogs.Info(
            summary + "\n\nDie eigenen Protokolle (_E) liegen jetzt in Haltungen_Verteilt und sind ueber "
            + "das Feld „Eigenes Protokoll“ (PDF_Eigen) verlinkt.",
            "Protokoll neu generieren");
    }
}
