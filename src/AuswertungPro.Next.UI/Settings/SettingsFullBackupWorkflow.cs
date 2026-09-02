using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
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
    Func<DateTime> UtcNow,
    /// <summary>
    /// Schreibt Grund und Umfang eines Fehlschlags ins Programmlog. Ohne das war
    /// die Ursache nur im Dialog sichtbar und nach dem Wegklicken verloren.
    /// null verwendet den zentralen Logkanal.
    /// </summary>
    Action<string>? Log = null);

public static class SettingsFullBackupWorkflow
{
    public static async Task RunAsync(
        SettingsFullBackupWorkflowRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var log = request.Log ?? (message => BestEffort.ReportWarning(message));

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
                // Der Grund gehoert ins Log, nicht nur in den Dialog: Nach dem
                // Wegklicken war bisher nirgends nachlesbar, WARUM die Sicherung
                // scheiterte.
                log($"[Datensicherung] Fehlgeschlagen (Ziel {targetRoot}): " +
                    $"{result.Error ?? "ohne Angabe"}");
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
                // Die Liste ist eine gedeckelte Stichprobe. Gemeldet wird die
                // tatsaechliche Zahl, damit eine grosse Luecke nicht klein aussieht.
                var anzahl = Math.Max(result.SkippedFileTotal, result.SkippedFiles.Count);
                foreach (var uebersprungen in result.SkippedFiles)
                    log($"[Datensicherung] Uebersprungen: {uebersprungen}");
                log($"[Datensicherung] Uebersprungene Dateien insgesamt: {anzahl}");

                var sample = string.Join(Environment.NewLine, result.SkippedFiles.Take(10));
                request.Dialogs.Warn(
                    $"Einige Dateien konnten nicht gesichert werden ({anzahl}).\n\n" +
                    $"{sample}\n\n" +
                    "Der bisherige Stand dieser Dateien bleibt in der Sicherung erhalten. " +
                    "Die vollstaendige Liste steht im Programmlog.",
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
            var userMessage = UserError.DescribeAndReport(ex, "Datensicherung");
            request.Operation.SetStatus($"Fehler: {userMessage}");
            request.Toasts.Error("Datensicherung fehlgeschlagen.");
            request.Dialogs.Error($"Datensicherung fehlgeschlagen:\n{userMessage}", "Datensicherung");
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
