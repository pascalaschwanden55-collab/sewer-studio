using System;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsCodexArtifactCleanupWorkflowRequest(
    CodexArtifactCleanupRequest CleanupRequest,
    ICodexArtifactCleanupService CleanupService,
    IDialogService Dialogs,
    IToastService Toasts,
    SettingsProgramCleanupWorkflowUi Ui);

public static class SettingsCodexArtifactCleanupWorkflow
{
    public static async Task RunAsync(SettingsCodexArtifactCleanupWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Ui.SetIsRunning(true);
        request.Ui.SetStatusText("Suche alte Agenten-Baukopien...");

        try
        {
            var report = await Task.Run(
                () => request.CleanupService.Analyze(request.CleanupRequest)).ConfigureAwait(true);

            if (report.Items.Count == 0)
            {
                request.Ui.SetStatusText("Keine alten Agenten-Baukopien gefunden.");
                request.Toasts.Info("Keine sicheren Codex-Baukopien zum Bereinigen gefunden.");
                return;
            }

            request.Ui.SetStatusText(
                $"Gefunden: {SettingsProgramCleanupPresentationBuilder.FormatBytes(report.TotalBytes)}");
            if (!request.Dialogs.ConfirmWarn(
                    SettingsCodexArtifactCleanupPresentationBuilder.BuildConfirmText(report),
                    "Alte Agenten-Daten bereinigen"))
            {
                request.Ui.SetStatusText("Bereinigung nicht gestartet.");
                return;
            }

            request.Ui.SetStatusText("Agenten-Daten werden bereinigt...");
            var result = await Task.Run(
                () => request.CleanupService.Clean(
                    request.CleanupRequest,
                    report.Items.Select(item => item.Path).ToArray())).ConfigureAwait(true);

            var successText = SettingsCodexArtifactCleanupPresentationBuilder.BuildSuccessText(result);
            request.Ui.SetStatusText(successText);

            if (result.FailedPaths.Count == 0)
            {
                request.Toasts.Success(successText);
                return;
            }

            request.Toasts.Warning("Bereinigung mit geschuetzten oder belegten Bereichen beendet.");
            var sample = string.Join(Environment.NewLine, result.FailedPaths.Take(8));
            request.Dialogs.Warn(
                $"{successText}\n\n{result.FailedPaths.Count} Bereich(e) blieben erhalten. " +
                $"Sie wurden geaendert, sind in Benutzung oder bestanden die Schutzpruefung nicht.\n\n{sample}",
                "Alte Agenten-Daten bereinigen");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Codex-Artefaktbereinigung");
            request.Ui.SetStatusText($"Fehler: {userMessage}");
            request.Toasts.Error("Agenten-Daten konnten nicht bereinigt werden.");
            request.Dialogs.Error(
                $"Agenten-Daten konnten nicht bereinigt werden:\n{userMessage}",
                "Alte Agenten-Daten bereinigen");
        }
        finally
        {
            request.Ui.SetIsRunning(false);
        }
    }
}
