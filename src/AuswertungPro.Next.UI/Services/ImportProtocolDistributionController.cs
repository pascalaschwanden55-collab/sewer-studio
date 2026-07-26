using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportProtocolDistributionActions(
    Func<string?> GetProjectFolder,
    Func<Project> GetProject,
    object CollectionLock,
    Func<bool> SaveProject);

/// <summary>
/// Steuert Auswahl, Hintergrundlauf und Ergebnisanzeige der Protokollverteilung.
/// </summary>
internal sealed class ImportProtocolDistributionController
{
    private readonly IDialogService _dialogs;
    private readonly INameBasedProtocolDistributor _distributor;
    private readonly ILogger _logger;

    public ImportProtocolDistributionController(
        IDialogService dialogs,
        INameBasedProtocolDistributor distributor,
        ILogger logger)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _distributor = distributor ?? throw new ArgumentNullException(nameof(distributor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(ImportProtocolDistributionActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var projectFolder = actions.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder))
        {
            _dialogs.Info("Kein Projekt geöffnet.", "Protokolle verteilen");
            return;
        }

        var sourceFolder = _dialogs.SelectFolder(
            "Verteil-Ordner mit Protokollen wählen",
            projectFolder);
        if (string.IsNullOrWhiteSpace(sourceFolder))
            return;

        var project = actions.GetProject();
        var report = await Task.Run(() =>
            _distributor.Distribute(project, projectFolder, sourceFolder, actions.CollectionLock));

        project.Dirty = true;
        var saved = ProjectSaveAttempt.Try(
            actions.SaveProject,
            "Protokollverteilung speichern",
            out var saveError);

        foreach (var message in report.Meldungen)
            _logger.LogWarning("Protokollverteilung: {Meldung}", message);

        var resultText = BuildResultText(report);
        if (!saved)
        {
            resultText +=
                "\n\nAenderungen uebernommen, aber nicht gespeichert. Bitte erneut speichern."
                + ProjectSaveAttempt.ErrorDetails(saveError);
            _dialogs.Warn(resultText, "Protokolle verteilen");
            return;
        }

        _dialogs.Info(resultText, "Protokolle verteilen");
    }

    private static string BuildResultText(ProtocolDistributionReport report)
    {
        var text = $"Verteilt: {report.HaltungProtokolle} Haltungs-Protokolle, "
            + $"{report.SchachtProtokolle} Schacht-Protokolle "
            + $"({report.SchaechteAngelegt} Schächte neu angelegt).";

        if (report.NichtZugeordnet.Count > 0)
        {
            text += $"\n\nNicht zugeordnet ({report.NichtZugeordnet.Count}):\n"
                + string.Join("\n", report.NichtZugeordnet.Take(30));
        }

        if (report.Meldungen.Count > 0)
        {
            text += $"\n\nFehler bei {report.Meldungen.Count} Datei(en). "
                + "Technische Details stehen im Tageslog.";
        }

        return text;
    }
}
