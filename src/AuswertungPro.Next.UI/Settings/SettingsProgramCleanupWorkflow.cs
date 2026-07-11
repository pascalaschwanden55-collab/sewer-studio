using System;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsProgramCleanupWorkflowUi(
    Action<bool> SetIsRunning,
    Action<string> SetStatusText);

public sealed record SettingsProgramCleanupWorkflowRequest(
    ProgramCleanupRequest CleanupRequest,
    ProgramCleanupService CleanupService,
    IDialogService Dialogs,
    IToastService Toasts,
    SettingsProgramCleanupWorkflowUi Ui);

public static class SettingsProgramCleanupWorkflow
{
    public static async Task RunAsync(SettingsProgramCleanupWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Ui.SetIsRunning(true);
        request.Ui.SetStatusText("Suche temporaere Programmdaten...");

        try
        {
            var report = await Task.Run(
                () => request.CleanupService.Analyze(request.CleanupRequest)).ConfigureAwait(true);

            if (report.Items.Count == 0)
            {
                request.Ui.SetStatusText("Keine bereinigbaren Programmdaten gefunden.");
                request.Toasts.Info("Keine temporaeren Programmdaten gefunden.");
                return;
            }

            request.Ui.SetStatusText(
                $"Gefunden: {SettingsProgramCleanupPresentationBuilder.FormatBytes(report.TotalBytes)}");
            if (!request.Dialogs.ConfirmWarn(
                    SettingsProgramCleanupPresentationBuilder.BuildConfirmText(report),
                    "Programmdaten bereinigen"))
            {
                request.Ui.SetStatusText("Bereinigung nicht gestartet.");
                return;
            }

            request.Ui.SetStatusText("Bereinigung laeuft...");
            var result = await Task.Run(
                () => request.CleanupService.Clean(request.CleanupRequest)).ConfigureAwait(true);

            var successText = SettingsProgramCleanupPresentationBuilder.BuildSuccessText(result);
            request.Ui.SetStatusText(successText);

            if (result.FailedPaths.Count == 0)
            {
                request.Toasts.Success(successText);
                return;
            }

            request.Toasts.Warning("Bereinigung mit einzelnen uebersprungenen Dateien beendet.");
            var sample = string.Join(Environment.NewLine, result.FailedPaths.Take(8));
            request.Dialogs.Warn(
                $"{successText}\n\n{result.FailedPaths.Count} Pfad(e) konnten nicht entfernt werden. " +
                $"Meist sind diese gerade in Benutzung.\n\n{sample}",
                "Programmdaten bereinigen");
        }
        catch (Exception ex)
        {
            request.Ui.SetStatusText($"Fehler: {ex.Message}");
            request.Toasts.Error("Programmbereinigung fehlgeschlagen.");
            request.Dialogs.Error(
                $"Programmbereinigung fehlgeschlagen:\n{ex.Message}",
                "Programmdaten bereinigen");
        }
        finally
        {
            request.Ui.SetIsRunning(false);
        }
    }
}
