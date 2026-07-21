using System;
using System.IO;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Leitet AppData fuer den GESAMTEN Infrastructure-Testlauf in ein Temp-Verzeichnis um
/// (gleiches Muster wie in den UI.Tests). Ohne dieses Netz loest
/// <see cref="AppDataPathResolver.Resolve"/> auf %LOCALAPPDATA%\SewerStudio auf — also die
/// ECHTE Nutzer-AppData samt settings.json. Ein Test, der darueber KnowledgeRoot ermittelt,
/// koennte den echten Wert (z. B. C:\KI_BRAIN) lesen und dorthin schreiben oder die echten
/// Einstellungen ueberschreiben.
///
/// Tests, die die Isolation gezielt aufheben wollen (AppDataPathResolverTests,
/// AiOptimizationSessionStoreTests), setzen die Env-Variable in ihrem eigenen Scope und
/// stellen danach diesen Temp-Wert wieder her — dieser Initializer bleibt damit kompatibel.
/// </summary>
internal static class TestAppDataIsolation
{
    [ModuleInitializer]
    internal static void UmleitenAufTempVerzeichnis()
    {
        // Nur setzen, wenn nicht bereits von aussen (z. B. CI) vorgegeben.
        if (!string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar)))
            return;

        var dir = Path.Combine(
            Path.GetTempPath(),
            "SewerStudioInfraTests_AppData_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable(AppDataPathResolver.AppDataDirEnvVar, dir);
    }
}
