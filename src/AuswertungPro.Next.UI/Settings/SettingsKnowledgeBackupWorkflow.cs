using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsKnowledgeBackupWorkflowRequest(
    IDialogService Dialogs,
    Action<string> SetStatusText,
    Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>> ExportAsync,
    Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>> ImportAsync,
    Func<DateTime> Now);

public static class SettingsKnowledgeBackupWorkflow
{
    public static Task ExportAsync(
        IDialogService dialogs,
        Action<string> setStatusText,
        Func<DateTime> now,
        CancellationToken ct = default)
        => ExportAsync(DefaultRequest(dialogs, setStatusText, now), ct);

    public static Task ImportAsync(
        IDialogService dialogs,
        Action<string> setStatusText,
        Func<DateTime> now,
        CancellationToken ct = default)
        => ImportAsync(DefaultRequest(dialogs, setStatusText, now), ct);

    public static Task ExportAsync(
        IKnowledgeBackupService knowledgeBackup,
        IDialogService dialogs,
        Action<string> setStatusText,
        Func<DateTime> now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBackup);
        return ExportAsync(DefaultRequest(dialogs, setStatusText, now, knowledgeBackup), ct);
    }

    public static Task ImportAsync(
        IKnowledgeBackupService knowledgeBackup,
        IDialogService dialogs,
        Action<string> setStatusText,
        Func<DateTime> now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgeBackup);
        return ImportAsync(DefaultRequest(dialogs, setStatusText, now, knowledgeBackup), ct);
    }

    public static async Task ExportAsync(
        SettingsKnowledgeBackupWorkflowRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var defaultName = $"SewerStudio_KI_Backup_{request.Now():yyyy-MM-dd}";
        var path = request.Dialogs.SaveFile(
            "KI-Wissen exportieren",
            "ZIP-Archiv (*.zip)|*.zip",
            ".zip",
            defaultName);
        if (path is null)
            return;

        request.SetStatusText("Exportiere...");
        try
        {
            var result = await request.ExportAsync(
                path,
                new Progress<string>(request.SetStatusText),
                ct).ConfigureAwait(true);

            if (result.Success)
            {
                var sizeMb = result.SizeBytes / (1024.0 * 1024.0);
                request.SetStatusText($"Export OK: {result.FileCount} Dateien, {sizeMb:F1} MB");
                request.Dialogs.Info(
                    $"KI-Wissen erfolgreich exportiert.\n\n" +
                    $"Dateien: {result.FileCount}\n" +
                    $"Groesse: {sizeMb:F1} MB\n" +
                    $"Pfad: {path}",
                    "SewerStudio");
            }
            else
            {
                request.SetStatusText($"Fehler: {result.Error}");
                request.Dialogs.Error($"Export fehlgeschlagen:\n{result.Error}", "SewerStudio");
            }
        }
        catch (OperationCanceledException)
        {
            request.SetStatusText("Export abgebrochen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "KI-Wissen exportieren");
            request.SetStatusText($"Fehler: {userMessage}");
            request.Dialogs.Error($"Export fehlgeschlagen:\n{userMessage}", "SewerStudio");
        }
    }

    public static async Task ImportAsync(
        SettingsKnowledgeBackupWorkflowRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = request.Dialogs.OpenFile(
            "KI-Wissen importieren",
            "ZIP-Archiv (*.zip)|*.zip");
        if (path is null)
            return;

        var confirm = request.Dialogs.Confirm(
            "Vorhandene KI-Daten und Einstellungen werden ueberschrieben.\n\n" +
            "Nach dem Import muss die Anwendung neu gestartet werden.\n\n" +
            "Fortfahren?",
            "SewerStudio");
        if (!confirm)
            return;

        request.SetStatusText("Importiere...");
        try
        {
            var result = await request.ImportAsync(
                path,
                new Progress<string>(request.SetStatusText),
                ct).ConfigureAwait(true);

            if (result.Success)
            {
                request.SetStatusText($"Import OK: {result.FileCount} Dateien");
                request.Dialogs.Info(
                    $"KI-Wissen erfolgreich importiert ({result.FileCount} Dateien).\n\n" +
                    "Bitte starten Sie die Anwendung neu.",
                    "SewerStudio");
            }
            else
            {
                request.SetStatusText($"Fehler: {result.Error}");
                request.Dialogs.Error($"Import fehlgeschlagen:\n{result.Error}", "SewerStudio");
            }
        }
        catch (OperationCanceledException)
        {
            request.SetStatusText("Import abgebrochen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "KI-Wissen importieren");
            request.SetStatusText($"Fehler: {userMessage}");
            request.Dialogs.Error($"Import fehlgeschlagen:\n{userMessage}", "SewerStudio");
        }
    }

    private static SettingsKnowledgeBackupWorkflowRequest DefaultRequest(
        IDialogService dialogs,
        Action<string> setStatusText,
        Func<DateTime> now,
        IKnowledgeBackupService? knowledgeBackup = null)
        => new(
            dialogs,
            setStatusText,
            knowledgeBackup is null ? KnowledgeBackupService.ExportAsync : knowledgeBackup.ExportAsync,
            knowledgeBackup is null ? KnowledgeBackupService.ImportAsync : knowledgeBackup.ImportAsync,
            now);
}
