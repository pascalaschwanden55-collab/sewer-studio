using System.Runtime.CompilerServices;

// Erlaubt dem UI-Test-Projekt den Zugriff auf internal-Typen (z. B. SettingsMigrator, SettingsQuarantine, SettingsStore).
[assembly: InternalsVisibleTo("AuswertungPro.Next.UI.Tests")]
