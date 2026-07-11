using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Maintenance;

namespace AuswertungPro.Next.UI.Settings;

public static class SettingsProgramCleanupPresentationBuilder
{
    public static string BuildConfirmText(ProgramCleanupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var categories = report.Items
            .GroupBy(item => item.Category)
            .OrderByDescending(group => group.Sum(item => item.SizeBytes))
            .Select(group => $"- {CategoryName(group.Key)}: {FormatBytes(group.Sum(item => item.SizeBytes))}")
            .ToArray();

        var largest = report.Items
            .Take(5)
            .Select(item =>
            {
                var displayPath = Path.GetRelativePath(report.ProgramRoot, item.Path);
                if (displayPath.StartsWith("..", StringComparison.Ordinal))
                    displayPath = item.Path;
                return $"- {displayPath} ({FormatBytes(item.SizeBytes)})";
            })
            .ToArray();

        return
            $"Gefunden: {FormatBytes(report.TotalBytes)} in {report.TotalFiles:N0} Dateien.\n\n" +
            string.Join(Environment.NewLine, categories) +
            "\n\nGroesste Bereiche:\n" +
            string.Join(Environment.NewLine, largest) +
            "\n\nGeschuetzt bleiben Projektdateien, Videos, PDF, Modelle, Trainingsdaten, " +
            "Karten, Einstellungen, Sicherungen, Git und Release-Pakete.\n\n" +
            "Diese temporaeren Daten jetzt endgueltig loeschen?";
    }

    public static string BuildSuccessText(ProgramCleanupResult result)
        => $"Bereinigt: {FormatBytes(result.FreedBytes)} freigegeben, " +
           $"{result.DeletedFiles:N0} Dateien entfernt.";

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024d * 1024 * 1024):0.00} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024d * 1024):0.0} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024d:0.0} KB";
        return bytes.ToString("N0", CultureInfo.CurrentCulture) + " Bytes";
    }

    private static string CategoryName(ProgramCleanupCategory category)
        => category switch
        {
            ProgramCleanupCategory.WorkspaceTemp => "Temporaere Arbeitsdaten",
            ProgramCleanupCategory.BuildOutput => "Build-Zwischendaten",
            ProgramCleanupCategory.PythonCache => "Python-Zwischenspeicher",
            _ => "Alte Windows-Temp-Dateien"
        };
}
