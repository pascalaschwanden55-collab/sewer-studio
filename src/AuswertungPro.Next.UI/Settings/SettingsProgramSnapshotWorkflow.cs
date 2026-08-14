using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsProgramSnapshotWorkflowRequest(
    IDialogService Dialogs,
    Action<string> SetStatusText,
    Func<string> GetProgramRoot,
    Func<ProgramSnapshotRequest, IProgress<string>?, CancellationToken, Task<ProgramSnapshotResult>> CreateAsync,
    Func<DateTime> Now);

/// <summary>
/// Fuehrt den Benutzer durch die Programm-Momentaufnahme: Ziel waehlen, packen,
/// Ergebnis melden. Enthaelt bewusst keine Auswahl- oder Dateilogik — die liegt
/// im <see cref="IProgramSnapshotService"/>.
/// </summary>
public static class SettingsProgramSnapshotWorkflow
{
    private const string DialogTitle = "Programm sichern";

    public static async Task RunAsync(
        SettingsProgramSnapshotWorkflowRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var programRoot = request.GetProgramRoot();
        if (string.IsNullOrWhiteSpace(programRoot))
        {
            request.Dialogs.Warn("Der Programmordner wurde nicht gefunden.", DialogTitle);
            return;
        }

        var defaultName = $"SewerStudio_Programm_{request.Now():yyyy-MM-dd}";
        var path = request.Dialogs.SaveFile(
            "Programm-Momentaufnahme speichern",
            "ZIP-Archiv (*.zip)|*.zip",
            ".zip",
            defaultName);
        if (path is null)
            return;

        request.SetStatusText("Programm wird gepackt...");
        try
        {
            var result = await request
                .CreateAsync(
                    new ProgramSnapshotRequest(programRoot, path),
                    new Progress<string>(request.SetStatusText),
                    ct)
                .ConfigureAwait(true);

            if (!result.Success)
            {
                request.SetStatusText($"Fehler: {result.Error}");
                request.Dialogs.Error($"Die Momentaufnahme ist fehlgeschlagen:\n{result.Error}", DialogTitle);
                return;
            }

            var sizeMb = result.SizeBytes / (1024.0 * 1024.0);
            var unreadable = result.UnreadableDirectoriesOrEmpty;

            request.SetStatusText(unreadable.Count > 0
                ? $"Programm gesichert mit {unreadable.Count} unlesbaren Ordnern: "
                  + $"{result.FileCount} Dateien, {sizeMb:F1} MB"
                : $"Programm gesichert: {result.FileCount} Dateien, {sizeMb:F1} MB");

            var skippedHint = result.SkippedReparsePoints > 0
                ? $"\nUebersprungene Verknuepfungen: {result.SkippedReparsePoints}"
                : string.Empty;
            var checksumHint = string.IsNullOrEmpty(result.ArchiveSha256)
                ? string.Empty
                : $"\nPruefsumme: {result.ArchiveSha256[..16]}... (vollstaendig in {Path.GetFileName(path)}.sha256)";

            // Eine Sicherung mit Luecken darf nicht wie eine vollstaendige aussehen.
            if (unreadable.Count > 0)
            {
                const int maxAnzeige = 10;
                var liste = string.Join("\n", unreadable.Take(maxAnzeige).Select(d => $"  - {d}"));
                var mehr = unreadable.Count > maxAnzeige
                    ? $"\n  ... und {unreadable.Count - maxAnzeige} weitere"
                    : string.Empty;

                request.Dialogs.Warn(
                    "Programm-Momentaufnahme erstellt, aber UNVOLLSTAENDIG.\n\n" +
                    $"Diese {unreadable.Count} Ordner konnten nicht gelesen werden und fehlen:\n" +
                    liste + mehr + "\n\n" +
                    $"Dateien: {result.FileCount}\n" +
                    $"Groesse: {sizeMb:F1} MB\n" +
                    $"Pfad: {path}{skippedHint}{checksumHint}\n\n" +
                    "Die unersetzlichen Ordner (Quellcode, Tests, Werkzeuge, Sidecar, Git-Verlauf) " +
                    "sind vollstaendig — sonst waere die Sicherung abgebrochen.",
                    DialogTitle);
                return;
            }

            request.Dialogs.Info(
                "Programm-Momentaufnahme erstellt.\n\n" +
                $"Dateien: {result.FileCount}\n" +
                $"Groesse: {sizeMb:F1} MB\n" +
                $"Pfad: {path}{skippedHint}{checksumHint}\n\n" +
                "Enthalten sind Quellcode, der vollstaendige Git-Verlauf und die Modellgewichte. " +
                "Build-Ausgabe, Python-Umgebung und Kartenkacheln fehlen bewusst — sie entstehen neu.",
                DialogTitle);
        }
        catch (OperationCanceledException)
        {
            request.SetStatusText("Momentaufnahme abgebrochen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Programm-Momentaufnahme");
            request.SetStatusText($"Fehler: {userMessage}");
            request.Dialogs.Error($"Die Momentaufnahme ist fehlgeschlagen:\n{userMessage}", DialogTitle);
        }
    }
}
