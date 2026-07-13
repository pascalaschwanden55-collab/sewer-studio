using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportSummaryExportActions(
    Func<string?> GetProjectPath,
    Func<Project> GetProject,
    Action<string> SetLastResult,
    Action<string> SetStatus);

/// <summary>Steuert die sichere Erzeugung des CSV-Importberichts.</summary>
internal sealed class ImportSummaryExportController
{
    private readonly IDialogService _dialogs;
    private readonly IImportSummaryExporter _exporter;
    private readonly ILogger _logger;

    public ImportSummaryExportController(
        IDialogService dialogs,
        IImportSummaryExporter exporter,
        ILogger logger)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Execute(ImportSummaryExportActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectPath = actions.GetProjectPath();
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _dialogs.Info("Bitte zuerst das Projekt speichern.", "Import-Report");
            return;
        }

        try
        {
            var path = _exporter.Export(projectPath, actions.GetProject());
            actions.SetLastResult($"Import-Report erstellt:\n{path}");
            actions.SetStatus("Import-Report erstellt");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import-Report konnte nicht geschrieben werden.");
            _dialogs.Error(
                "Der Import-Report konnte nicht erstellt werden. Technische Details stehen im Tageslog.",
                "Import-Report");
        }
    }
}
