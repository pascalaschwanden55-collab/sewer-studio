using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsFullBackupWorkflowRequest(
    AppSettings Settings,
    IFullBackupService FullBackup,
    IDialogService Dialogs,
    IToastService Toasts,
    FullBackupOperationState Operation,
    Action FlushPendingSave,
    Action SaveSettingsImmediate,
    Func<DateTime> UtcNow);

public static class SettingsFullBackupWorkflow
{
    public static async Task RunAsync(
        SettingsFullBackupWorkflowRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Operation.IsRunning)
        {
            request.Toasts.Info("Datensicherung laeuft bereits.");
            return;
        }

        var targetFolder = request.Dialogs.SelectFolder(
            "Zielordner fuer die Datensicherung waehlen",
            request.Settings.LastFullBackupPath);
        if (targetFolder is null)
            return;

        if (!request.Operation.TryBegin(ct, out var runToken))
        {
            request.Toasts.Info("Datensicherung laeuft bereits.");
            return;
        }

        try
        {
            var report = await Task.Run(
                () => request.FullBackup.AnalyzeAsync(progress: null, runToken),
                runToken).ConfigureAwait(true);

            var targetRoot = Path.Combine(targetFolder, BackupPlanBuilder.TargetFolderName);
            var confirmText = SettingsFullBackupPresentationBuilder.BuildConfirmText(report, targetRoot);
            if (!request.Dialogs.Confirm(confirmText, "Datensicherung erstellen"))
            {
                request.Operation.SetStatus("Datensicherung nicht gestartet.");
                return;
            }

            request.FlushPendingSave();
            request.Operation.SetStatus("Datensicherung laeuft...");

            var progress = new InlineProgress<FullBackupProgress>(p =>
            {
                var presentation = SettingsFullBackupPresentationBuilder.BuildProgress(p);
                request.Operation.UpdateProgress(
                    presentation.Percent,
                    presentation.CurrentFileName,
                    presentation.StatusText);
            });

            var result = await Task.Run(
                () => request.FullBackup.RunAsync(targetFolder, progress, runToken),
                runToken).ConfigureAwait(true);

            if (!result.Success)
            {
                request.Operation.SetStatus($"Fehler: {result.Error}");
                request.Toasts.Error("Datensicherung fehlgeschlagen.");
                request.Dialogs.Error(result.Error ?? "Datensicherung fehlgeschlagen.", "Datensicherung");
                return;
            }

            var databaseInfo = result.DatabasesSnapshotted switch
            {
                1 => ", 1 Datenbank-Schnappschuss",
                > 1 => $", {result.DatabasesSnapshotted} Datenbank-Schnappschuesse",
                _ => string.Empty
            };
            request.Operation.UpdateProgress(
                100,
                string.Empty,
                $"Fertig: {result.FilesCopied} kopiert, {result.FilesVerified} vollstaendig geprueft" +
                $"{databaseInfo}, {result.FilesUnchanged} unveraendert, " +
                $"{result.FilesDeleted} nach {BackupVersionRetention.VersionsFolderName} verschoben.");
            request.Toasts.Success("Datensicherung abgeschlossen.");

            request.Settings.LastFullBackupUtc = request.UtcNow();
            request.Settings.LastFullBackupPath = targetFolder;
            request.Settings.LastFullBackupSizeBytes = result.TotalBytes;
            request.SaveSettingsImmediate();
            request.Operation.SetLastBackupInfo(SettingsFullBackupPresentationBuilder.BuildLastBackupInfo(
                request.Settings.LastFullBackupUtc,
                request.Settings.LastFullBackupPath,
                request.Settings.LastFullBackupSizeBytes));

            if (result.SkippedFiles.Count > 0)
            {
                var sample = string.Join(Environment.NewLine, result.SkippedFiles.Take(10));
                request.Dialogs.Warn(
                    $"Einige Dateien konnten nicht gesichert werden ({result.SkippedFiles.Count}).\n\n{sample}",
                    "Datensicherung");
            }
        }
        catch (OperationCanceledException)
        {
            request.Operation.UpdateProgress(
                request.Operation.Percent,
                string.Empty,
                "Abgebrochen - bereits Kopiertes bleibt erhalten.");
            request.Toasts.Info("Datensicherung abgebrochen.");
        }
        catch (Exception ex)
        {
            request.Operation.SetStatus($"Fehler: {ex.Message}");
            request.Toasts.Error("Datensicherung fehlgeschlagen.");
            request.Dialogs.Error($"Datensicherung fehlgeschlagen:\n{ex.Message}", "Datensicherung");
        }
        finally
        {
            request.Operation.Finish();
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
            => _handler = handler ?? throw new ArgumentNullException(nameof(handler));

        public void Report(T value) => _handler(value);
    }
}
