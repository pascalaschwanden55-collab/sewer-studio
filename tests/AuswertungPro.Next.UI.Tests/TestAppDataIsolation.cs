using System;
using System.IO;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Leitet AppData fuer den GESAMTEN Testlauf in ein Temp-Verzeichnis um.
/// Hintergrund: Tests, die Settings.Save() ausloesen (z.B. ueber
/// ShellViewModel.TryOpenProject), haben sonst die ECHTE settings.json des
/// Users unter %LOCALAPPDATA%\SewerStudio ueberschrieben — dabei gingen
/// Projekt-Merkliste und Einstellungen verloren.
/// </summary>
internal static class TestAppDataIsolation
{
    [ModuleInitializer]
    internal static void UmleitenAufTempVerzeichnis()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "SewerStudioTests_AppData_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, dir);
    }
}
