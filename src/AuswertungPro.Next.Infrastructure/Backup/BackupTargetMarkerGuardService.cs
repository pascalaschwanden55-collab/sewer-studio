using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Schuetzt die Vollsicherung davor, einen fremden, nicht markierten Zielordner zu verwenden.
/// </summary>
public sealed class BackupTargetMarkerGuardService : IBackupTargetMarkerGuard
{
    public string? ValidateAndCreateMarker(string backupRoot)
    {
        var root = Path.GetFullPath(backupRoot);
        var markerPath = Path.Combine(root, BackupPlanBuilder.MarkerFileName);

        if (Directory.Exists(root))
        {
            if (File.Exists(markerPath))
                return null;

            if (Directory.EnumerateFileSystemEntries(root).Any())
            {
                return $"Der Ordner \"{root}\" enthaelt bereits Daten, ist aber keine " +
                       "SewerStudio-Datensicherung (Marker-Datei fehlt). " +
                       "Bitte einen leeren Ordner oder eine bestehende Sicherung waehlen.";
            }
        }

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                markerPath,
                "SewerStudio-Datensicherung. Diese Datei markiert den Spiegel-Ordner \u2014 nicht loeschen.");
            File.SetAttributes(markerPath, FileAttributes.Hidden);
            return null;
        }
        catch (Exception ex)
        {
            return $"Zielordner \"{root}\" kann nicht beschrieben werden: {ex.Message}";
        }
    }
}
