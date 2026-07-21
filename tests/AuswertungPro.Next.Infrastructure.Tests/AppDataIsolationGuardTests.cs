using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert das laufweite AppData-Netz aus <see cref="TestAppDataIsolation"/>:
/// Der Infrastructure-Testlauf darf NIE auf die echte Nutzer-AppData
/// (%LOCALAPPDATA%\SewerStudio) zeigen — sonst koennte ein Test die echte settings.json
/// (inkl. KnowledgeRoot wie C:\KI_BRAIN) lesen oder ueberschreiben.
///
/// In der EnvironmentVars-Collection, damit die Env-Var-manipulierenden Tests nicht
/// parallel dazwischenfunken.
/// </summary>
[Collection("EnvironmentVars")]
public sealed class AppDataIsolationGuardTests
{
    [Fact]
    public void Testlauf_zeigt_nicht_auf_die_echte_AppData()
    {
        var echteAppData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataPathResolver.DefaultProductName);

        var aufgeloest = AppDataPathResolver.Resolve();

        Assert.NotEqual(echteAppData, aufgeloest, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppData_Env_ist_gesetzt_und_verweist_auf_ein_existierendes_Verzeichnis()
    {
        var dir = Environment.GetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar);

        Assert.False(string.IsNullOrWhiteSpace(dir));
        Assert.True(Directory.Exists(dir));
    }
}
