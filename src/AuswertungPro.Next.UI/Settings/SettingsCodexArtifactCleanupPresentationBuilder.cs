using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Maintenance;

namespace AuswertungPro.Next.UI.Settings;

public static class SettingsCodexArtifactCleanupPresentationBuilder
{
    public static string BuildConfirmText(CodexArtifactCleanupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var largest = report.Items
            .Take(5)
            .Select(item =>
                $"- {Path.GetFileName(item.Path)} " +
                $"({SettingsProgramCleanupPresentationBuilder.FormatBytes(item.SizeBytes)})")
            .ToArray();

        return
            $"Gefunden: {SettingsProgramCleanupPresentationBuilder.FormatBytes(report.TotalBytes)} " +
            $"in {report.Items.Count:N0} alten Agenten-Baukopien.\n\n" +
            "Groesste Bereiche:\n" +
            string.Join(Environment.NewLine, largest) +
            "\n\nEntfernt werden nur Bereiche, die seit mindestens 24 Stunden nicht geaendert wurden " +
            "und ausschliesslich bin-, obj- oder TestResults-Daten enthalten.\n\n" +
            "Aktuelle Bereiche sowie originale Projektdateien, Modelle, Karten, Verknuepfungen und unbekannte Inhalte " +
            "bleiben geschuetzt.\n\n" +
            "Diese alten Agenten-Baukopien jetzt endgueltig loeschen?";
    }

    public static string BuildSuccessText(CodexArtifactCleanupResult result)
        => $"Agenten-Daten bereinigt: " +
           $"{SettingsProgramCleanupPresentationBuilder.FormatBytes(result.FreedBytes)} freigegeben, " +
           $"{result.DeletedDirectories:N0} Bereiche entfernt.";
}
