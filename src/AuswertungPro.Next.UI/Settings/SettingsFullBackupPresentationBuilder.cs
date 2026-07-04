using System;
using System.IO;
using System.Text;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsFullBackupProgressPresentation(
    double Percent,
    string CurrentFileName,
    string StatusText);

public static class SettingsFullBackupPresentationBuilder
{
    public static string BuildConfirmText(FullBackupSizeReport report, string targetRoot)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine("Diese Datensicherung erstellt einen inkrementellen Spiegel.");
        sb.AppendLine();
        foreach (var component in report.Components)
        {
            var status = component.SourceFound ? string.Empty : " (Quelle nicht gefunden)";
            sb.AppendLine($"- {component.Name}: {ByteSizeFormatter.Format(component.Bytes)} / {component.FileCount} Dateien{status}");
        }

        sb.AppendLine();
        sb.AppendLine($"Gesamt: {ByteSizeFormatter.Format(report.TotalBytes)} / {report.TotalFiles} Dateien");
        sb.AppendLine($"Ziel: {targetRoot}");
        sb.AppendLine();
        sb.AppendLine("Hinweis: Im Sicherungsordner wird Verwaistes entfernt.");
        sb.AppendLine("Projekte und Videos sind nicht enthalten.");
        sb.AppendLine("NTFS/exFAT-Ziel empfohlen (FAT32: 4-GB-Grenze).");
        return sb.ToString();
    }

    public static SettingsFullBackupProgressPresentation BuildProgress(FullBackupProgress progress)
    {
        var percent = progress.BytesTotal <= 0
            ? 0
            : Math.Clamp(100.0 * progress.BytesDone / progress.BytesTotal, 0, 100);

        var currentFileName = string.IsNullOrWhiteSpace(progress.CurrentFile)
            ? string.Empty
            : Path.GetFileName(progress.CurrentFile);

        return new SettingsFullBackupProgressPresentation(
            percent,
            currentFileName,
            $"{progress.Component}: {progress.FilesDone} von {progress.FilesTotal} Dateien");
    }

    public static string BuildLastBackupInfo(DateTime? lastBackupUtc, string? lastBackupPath, long? sizeBytes)
    {
        if (lastBackupUtc is null || string.IsNullOrWhiteSpace(lastBackupPath))
            return "Noch keine Datensicherung erstellt.";

        var localTime = DateTime.SpecifyKind(lastBackupUtc.Value, DateTimeKind.Utc).ToLocalTime();
        var size = sizeBytes is long bytes
            ? ByteSizeFormatter.Format(bytes)
            : "Groesse unbekannt";

        return $"Letzte Datensicherung: {localTime:dd.MM.yyyy HH:mm} - {size} - {lastBackupPath}";
    }
}
