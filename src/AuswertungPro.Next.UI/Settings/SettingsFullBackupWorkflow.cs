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
    Action<string>? Log = null,
    /// <summary>
    /// Aufgabe 13 (Windows-Integration, 28.09.2026): spiegelt den Fortschritt zusaetzlich am
    /// Programmsymbol in der Taskleiste. Optional, damit bestehende Aufrufer/Tests unveraendert
    /// bleiben; null bedeutet "keine Anzeige".
    /// </summary>
    ITaskbarFortschritt? Taskbar = null);

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
            request.Toasts.Info("Datensicherung läuft bereits.");
            return;
        }

        var targetFolder = request.Dialogs.SelectFolder(
            "Zielordner für die Datensicherung wählen",
            request.Settings.LastFullBackupPath);
        if (targetFolder is null)
            return;

        if (!request.Operation.TryBegin(ct, out var runToken))
        {
            request.Toasts.Info("Datensicherung läuft bereits.");
            return;
        }

        var taskbar = request.Taskbar;
        SicherTaskbar(() => taskbar?.SetzeUnbestimmt());

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
                SicherTaskbar(() => taskbar?.Beenden());
                return;
            }

            request.FlushPendingSave();
            request.Operation.SetStatus("Datensicherung läuft...");

            var progress = new InlineProgress<FullBackupProgress>(p =>
            {
                var presentation = SettingsFullBackupPresentationBuilder.BuildProgress(p);
                request.Operation.UpdateProgress(
                    presentation.Percent,
                    presentation.CurrentFileName,
                    presentation.StatusText);
                // Dieser Callback laeuft auf dem Threadpool-Thread der Datensicherung (kein
                // Progress<T> mit eigenem Marshalling) - SicherTaskbar faengt zusaetzlich zu
                // ITaskbarFortschritt.Anwenden(...) selbst jede Ausnahme ab: eine Anzeige darf
                // den Lauf nie abbrechen, egal welche Implementierung injiziert wurde.
                SicherTaskbar(() => taskbar?.SetzeFortschritt(presentation.Percent / 100d));
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
                SicherTaskbar(() => taskbar?.Fehler());
                request.Dialogs.Error(result.Error ?? "Datensicherung fehlgeschlagen.", "Datensicherung");
                // Der rote Zustand bleibt WAEHREND der Dialog offen ist sichtbar stehen (der
                // Benutzer soll den Fehlschlag am Symbol bemerken); erst nach dem Wegklicken
                // (Dialogs.Error ist modal/blockierend) wird zurueckgesetzt (MINOR 4, Fix-Runde 1).
                SicherTaskbar(() => taskbar?.Beenden());
                return;
            }

            var databaseInfo = result.DatabasesSnapshotted switch
            {
                1 => ", 1 Datenbank-Schnappschuss",
                > 1 => $", {result.DatabasesSnapshotted} Datenbank-Schnappschüsse",
                _ => string.Empty
            };
            request.Operation.UpdateProgress(
                100,
                string.Empty,
                $"Fertig: {result.FilesCopied} kopiert, {result.FilesVerified} vollständig geprüft" +
                $"{databaseInfo}, {result.FilesUnchanged} unverändert, " +
                $"{result.FilesDeleted} entfernt.");
            if (result.SkippedFileTotal > 0 || result.SkippedFiles.Count > 0)
                request.Toasts.Warning("Datensicherung mit Lücken abgeschlossen – Hinweise prüfen.");
            else
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
                    log($"[Datensicherung] Übersprungen: {uebersprungen}");
                log($"[Datensicherung] Übersprungene Dateien insgesamt: {anzahl}");

                var sample = string.Join(Environment.NewLine, result.SkippedFiles.Take(10));
                request.Dialogs.Warn(
                    $"Einige Dateien konnten nicht gesichert werden ({anzahl}).\n\n" +
                    $"{sample}\n\n" +
                    "Vorhandene ältere Kopien bleiben erhalten. Ohne ältere Kopie fehlt die Datei in der Sicherung. " +
                    "Die vollständige Liste steht im Sicherungsprotokoll «SewerStudio_Sicherung_Protokoll.txt» neben dem Sicherungsordner.",
                    "Datensicherung");
            }

            SicherTaskbar(() => taskbar?.Beenden());
        }
        catch (OperationCanceledException)
        {
            request.Operation.UpdateProgress(
                request.Operation.Percent,
                string.Empty,
                "Abgebrochen - vorheriger Sicherungsstand wiederhergestellt.");
            request.Toasts.Info("Datensicherung abgebrochen.");
            // Ein Abbruch durch den Benutzer ist kein Fehler (MINOR 5-Prinzip, hier bereits
            // vorher korrekt): sofort zurueck auf "keine Anzeige", kein roter Zustand.
            SicherTaskbar(() => taskbar?.Beenden());
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Datensicherung");
            request.Operation.SetStatus($"Fehler: {userMessage}");
            request.Toasts.Error("Datensicherung fehlgeschlagen.");
            SicherTaskbar(() => taskbar?.Fehler());
            request.Dialogs.Error($"Datensicherung fehlgeschlagen:\n{userMessage}", "Datensicherung");
            // Wie beim Ergebnis-Fehlschlag oben: rot bleibt sichtbar, bis der Dialog
            // bestaetigt ist, dann zurueckgesetzt (MINOR 4, Fix-Runde 1).
            SicherTaskbar(() => taskbar?.Beenden());
        }
        finally
        {
            request.Operation.Finish();
        }
    }

    /// <summary>
    /// Eine Taskleisten-Anzeige darf einen Sicherungslauf NIE abbrechen (CRITICAL 1, Fix-Runde 1
    /// - real reproduziert: ein auf dem Threadpool-Thread geworfener Zugriff auf
    /// Window.TaskbarItemInfo liess den kompletten Lauf als Fehlschlag werten). Diese Sperre gilt
    /// zusaetzlich zu <see cref="TaskbarFortschritt"/>s eigenem Schutz - unabhaengig davon, welche
    /// ITaskbarFortschritt-Implementierung injiziert wurde.
    /// </summary>
    private static void SicherTaskbar(Action? aufruf)
    {
        if (aufruf is null)
            return;

        try
        {
            aufruf();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"Taskleisten-Fortschritt konnte nicht ausgefuehrt werden: {ex.Message}");
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
