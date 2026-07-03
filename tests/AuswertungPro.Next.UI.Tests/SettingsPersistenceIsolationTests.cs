using System;
using System.IO;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die Testlauf-Isolation ab: Kein Test darf in die echte
/// %LOCALAPPDATA%\SewerStudio\settings.json des Users schreiben.
/// </summary>
public sealed class SettingsPersistenceIsolationTests
{
    [Fact]
    public void AppDataDir_ZeigtImTestlaufNichtAufDasEchteUserProfil()
    {
        var echtesProfil = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SewerStudio");

        Assert.False(
            string.Equals(AppSettings.AppDataDir, echtesProfil, StringComparison.OrdinalIgnoreCase),
            $"Testlauf zeigt auf echtes User-Profil: {AppSettings.AppDataDir} — TestAppDataIsolation greift nicht.");
    }
}
