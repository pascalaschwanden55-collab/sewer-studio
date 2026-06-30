namespace AuswertungPro.Next.Application.Ai.Backup;

/// <summary>
/// Richtlinien zur Versionskompatibilitaet von Backup-Manifesten.
/// Reine Logik ohne IO-Abhaengigkeiten.
/// </summary>
public static class BackupManifestVersionPolicy
{
    /// <summary>Aktuelle Manifest-Versionsnummer, die beim Export geschrieben wird.</summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Gibt true zurueck wenn das Backup-Archiv mit der aktuellen Software-Version
    /// importiert werden kann, false wenn die Archiv-Version zu neu ist.
    /// </summary>
    /// <param name="archiveVersion">Versionsnummer aus dem _manifest.json des Archivs.</param>
    public static bool IsCompatible(int archiveVersion)
        => archiveVersion <= CurrentVersion;

    /// <summary>
    /// Erzeugt einen deutsch-sprachigen Fehlermeldungstext fuer inkompatible Versionen.
    /// </summary>
    public static string FormatIncompatibleMessage(int archiveVersion)
        => $"Backup-Version {archiveVersion} ist neuer als die aktuelle Version {CurrentVersion}. " +
           "Bitte aktualisieren Sie die Software.";
}
