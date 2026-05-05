using System;
using System.IO;
using System.Reflection;

namespace AuswertungPro.Next.UI.Modules;

/// <summary>
/// Phase 5.2.D: VSA-Katalog-Pfad-Resolution aus ServiceProvider extrahiert.
///
/// Kapselt die Lookup-Logik fuer EN13508_VSA_CH_DEU_*.xml-Files an
/// vier moeglichen Quellen:
///   1. Settings.VsaCatalogSecXmlPath / VsaCatalogNodXmlPath
///   2. Environment-Variablen VSA_CATALOG_*
///   3. LastProjectPath/DISK1/System/ProgramData/CDLAB/...
///   4. WinCanCatalogDirectory + Auto-Detect-Pfade
///
/// Reine statische Helfer ohne Logger-Bindung.
/// </summary>
internal static class VsaCatalogResolver
{
    public static string? ResolveSecPath(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.VsaCatalogSecXmlPath))
        {
            if (File.Exists(settings.VsaCatalogSecXmlPath))
                return settings.VsaCatalogSecXmlPath;

            if (Directory.Exists(settings.VsaCatalogSecXmlPath))
            {
                var fromDir = FindSecCatalogInRoot(settings.VsaCatalogSecXmlPath);
                if (!string.IsNullOrWhiteSpace(fromDir))
                    return fromDir;
            }
        }

        var env = Environment.GetEnvironmentVariable("VSA_CATALOG_SEC_XML");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var envRoot = Environment.GetEnvironmentVariable("VSA_CATALOG_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
        {
            var fromRoot = FindSecCatalogInRoot(envRoot);
            if (!string.IsNullOrWhiteSpace(fromRoot))
                return fromRoot;
        }

        if (!string.IsNullOrWhiteSpace(settings.LastProjectPath))
        {
            var candidate = Path.Combine(
                settings.LastProjectPath,
                "DISK1",
                "System",
                "ProgramData",
                "CDLAB",
                "Common",
                "Catalogs",
                "Version4",
                "EN13508_VSA_CH_DEU_SEC.xml");
            if (File.Exists(candidate))
                return candidate;

            var fromProject = FindSecCatalogInRoot(Path.Combine(
                settings.LastProjectPath,
                "DISK1",
                "System",
                "ProgramData",
                "CDLAB",
                "Common",
                "Catalogs"));
            if (!string.IsNullOrWhiteSpace(fromProject))
                return fromProject;
        }

        if (!string.IsNullOrWhiteSpace(settings.WinCanCatalogDirectory))
        {
            var fromWinCan = FindSecCatalogInRoot(settings.WinCanCatalogDirectory);
            if (!string.IsNullOrWhiteSpace(fromWinCan))
                return fromWinCan;
        }

        var commonPaths = new[]
        {
            @"C:\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
            @"C:\Program Files\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
            @"C:\Program Files (x86)\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs"
        };
        foreach (var commonPath in commonPaths)
        {
            var fromCommon = FindSecCatalogInRoot(commonPath);
            if (!string.IsNullOrWhiteSpace(fromCommon))
                return fromCommon;
        }

        return null;
    }

    public static string? ResolveNodPath(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.VsaCatalogNodXmlPath))
        {
            if (File.Exists(settings.VsaCatalogNodXmlPath))
                return settings.VsaCatalogNodXmlPath;

            if (Directory.Exists(settings.VsaCatalogNodXmlPath))
            {
                var fromDir = FindNodCatalogInRoot(settings.VsaCatalogNodXmlPath);
                if (!string.IsNullOrWhiteSpace(fromDir))
                    return fromDir;
            }
        }

        var env = Environment.GetEnvironmentVariable("VSA_CATALOG_NOD_XML");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var envRoot = Environment.GetEnvironmentVariable("VSA_CATALOG_NOD_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
        {
            var fromRoot = FindNodCatalogInRoot(envRoot);
            if (!string.IsNullOrWhiteSpace(fromRoot))
                return fromRoot;
        }

        return null;
    }

    public static string? ResolveTextPath(AppSettings settings, string? resolvedXmlPath)
    {
        var configured = settings.VsaCatalogSecXmlPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                var dir = Path.GetDirectoryName(configured);
                var fromDir = FindTextCatalogInRoot(dir);
                if (!string.IsNullOrWhiteSpace(fromDir))
                    return fromDir;
            }
            else if (Directory.Exists(configured))
            {
                var fromRoot = FindTextCatalogInRoot(configured);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                    return fromRoot;
            }
        }

        if (!string.IsNullOrWhiteSpace(resolvedXmlPath))
        {
            var dir = Path.GetDirectoryName(resolvedXmlPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var parent = Directory.GetParent(dir);
                if (parent is not null && string.Equals(Path.GetFileName(dir), "Version4", StringComparison.OrdinalIgnoreCase))
                {
                    var fromRoot = FindTextCatalogInRoot(parent.FullName);
                    if (!string.IsNullOrWhiteSpace(fromRoot))
                        return fromRoot;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Stellt sicher, dass die eingebettete vsa_codes.json am Zielpfad liegt.
    /// Bei Fehler stiller Fallback (JsonCodeCatalogProvider liefert eigenen Fallback).
    /// </summary>
    public static void EnsureEmbeddedCatalogFile(string targetPath)
    {
        if (File.Exists(targetPath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var asm = Assembly.GetExecutingAssembly();
            const string resourceName = "AuswertungPro.Next.UI.Data.vsa_codes.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null)
                return;

            using var fs = File.Create(targetPath);
            stream.CopyTo(fs);
        }
        catch
        {
            // ignore, fallback handled by JsonCodeCatalogProvider
        }
    }

    private static string? FindSecCatalogInRoot(string root)
    {
        var v4 = Path.Combine(root, "Version4");
        var candidates = new[]
        {
            Path.Combine(root, "EN13508_VSA-2019_CH_DEU_SEC.xml"),
            Path.Combine(root, "EN13508_VSA_CH_DEU_SEC.xml"),
            Path.Combine(v4, "EN13508_VSA-2019_CH_DEU_SEC.xml"),
            Path.Combine(v4, "EN13508_VSA_CH_DEU_SEC.xml"),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    private static string? FindNodCatalogInRoot(string root)
    {
        var v4 = Path.Combine(root, "Version4");
        var candidates = new[]
        {
            Path.Combine(v4, "EN13508_VSA-2019_CH_DEU_NOD.xml"),
            Path.Combine(v4, "EN13508_VSA_CH_DEU_NOD.xml"),
            Path.Combine(root, "EN13508_VSA-2019_CH_DEU_NOD.xml"),
            Path.Combine(root, "EN13508_VSA_CH_DEU_NOD.xml")
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    private static string? FindTextCatalogInRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        var candidates = new[]
        {
            Path.Combine(root, "EN13508_VSA_CH_DEU_SEC.xml"),
            Path.Combine(root, "EN13508_VSA-2019_CH_DEU_SEC.xml")
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }
}
