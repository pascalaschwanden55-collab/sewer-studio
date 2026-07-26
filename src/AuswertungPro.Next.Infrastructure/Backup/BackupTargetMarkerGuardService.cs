using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Schuetzt die Vollsicherung davor, einen fremden, nicht markierten Zielordner zu verwenden.
/// </summary>
public sealed class BackupTargetMarkerGuardService : IBackupTargetMarkerGuard
{
    internal const string MarkerContent =
        "SewerStudio-Datensicherung. Diese Datei markiert den Spiegel-Ordner \u2014 nicht loeschen.";

    public string? ValidateAndCreateMarker(string backupRoot)
    {
        var root = Path.GetFullPath(backupRoot);
        BackupTargetPathGuard.EnsureRootIsSafe(root);
        var markerPath = BackupTargetPathGuard.ResolveRelativePath(
            root,
            BackupPlanBuilder.MarkerFileName);

        if (Directory.Exists(root))
        {
            if (File.Exists(markerPath))
            {
                BackupTargetPathGuard.EnsurePathIsSafe(root, markerPath);
                var content = File.ReadAllText(markerPath);
                return string.Equals(content, MarkerContent, StringComparison.Ordinal)
                    ? null
                    : "Die Sicherungs-Marker-Datei ist ungueltig. Aus Sicherheitsgruenden wurde nichts veraendert.";
            }

            if (Directory.EnumerateFileSystemEntries(root).Any())
            {
                return $"Der Ordner \"{root}\" enthaelt bereits Daten, ist aber keine " +
                       "SewerStudio-Datensicherung (Marker-Datei fehlt). " +
                       "Bitte einen leeren Ordner oder eine bestehende Sicherung waehlen.";
            }
        }

        try
        {
            BackupTargetPathGuard.EnsureRootIsSafe(root);
            Directory.CreateDirectory(root);
            BackupTargetPathGuard.EnsureRootIsSafe(root);
            BackupTargetPathGuard.EnsurePathIsSafe(root, markerPath);
            File.WriteAllText(
                markerPath,
                MarkerContent);
            BackupTargetPathGuard.EnsurePathIsSafe(root, markerPath);
            File.SetAttributes(markerPath, FileAttributes.Hidden);
            return null;
        }
        catch (Exception ex)
        {
            return $"Zielordner \"{root}\" kann nicht beschrieben werden: {ex.Message}";
        }
    }
}
