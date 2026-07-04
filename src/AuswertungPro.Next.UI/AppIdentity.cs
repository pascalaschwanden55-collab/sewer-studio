namespace AuswertungPro.Next.UI;

public static class AppIdentity
{
    public const string ProductName = "SewerStudio";

    // Zentrale Versionsnummer der Anwendung — einzige Quelle der Wahrheit.
    // Wird in der Startanimation und in den Einstellungen angezeigt.
    public const string Version = "4.5";

    // Anzeige-Variante mit "v"-Praefix (z.B. fuer Splash und Einstellungen).
    public const string DisplayVersion = "v" + Version;

    // Legacy folder names used by earlier versions.
    public const string LegacyLocalDataFolder = "AuswertungPro.Next";
    public const string LegacyRoamingDataFolder = "AuswertungPro";
}
