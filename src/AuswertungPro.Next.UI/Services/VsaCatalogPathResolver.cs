using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Services;

public sealed record VsaCatalogPathResolution(
    string? SectionCatalogPath,
    string? NodeCatalogPath,
    string? KekManifestPath,
    IReadOnlyList<string> XmlCatalogPaths,
    IReadOnlyList<string> SourcePaths,
    string? DisplayPath);

public static class VsaCatalogPathResolver
{
    public const string SectionCatalogPathEnvVar = "VSA_CATALOG_SEC_XML";
    public const string SectionCatalogRootEnvVar = "VSA_CATALOG_ROOT";
    public const string NodeCatalogPathEnvVar = "VSA_CATALOG_NOD_XML";
    public const string NodeCatalogRootEnvVar = "VSA_CATALOG_NOD_ROOT";
    public const string KekManifestPathEnvVar = "VSA_KEK_2020_CATALOG_MANIFEST";
    public const string KekManifestFileName = "vsa_kek_2020_catalog_manifest.json";

    public static VsaCatalogPathResolution Resolve(
        AppSettings settings,
        string? baseDirectory = null,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var getEnv = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        var sectionCatalogPath = ResolveSectionCatalogPath(settings, getEnv);
        var nodeCatalogPath = ResolveNodeCatalogPath(settings, getEnv);
        var kekManifestPath = ResolveKekManifestPath(baseDirectory ?? AppContext.BaseDirectory, getEnv);

        var xmlCatalogPaths = new[] { sectionCatalogPath, nodeCatalogPath }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sourcePaths = new[] { kekManifestPath }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Concat(xmlCatalogPaths)
            .ToArray();

        return new VsaCatalogPathResolution(
            sectionCatalogPath,
            nodeCatalogPath,
            kekManifestPath,
            xmlCatalogPaths,
            sourcePaths,
            sourcePaths.Length > 0 ? string.Join(" | ", sourcePaths) : null);
    }

    public static string? ResolveTextFallbackPath(AppSettings settings, string? resolvedXmlPath)
    {
        ArgumentNullException.ThrowIfNull(settings);

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

    private static string? ResolveSectionCatalogPath(AppSettings settings, Func<string, string?> getEnv)
    {
        if (!string.IsNullOrWhiteSpace(settings.VsaCatalogSecXmlPath))
        {
            if (IsCanonicalVsa2019Catalog(settings.VsaCatalogSecXmlPath, Vsa2019CatalogResolver.SectionCatalogFileName))
                return settings.VsaCatalogSecXmlPath;

            if (Directory.Exists(settings.VsaCatalogSecXmlPath))
            {
                var fromDir = Vsa2019CatalogResolver.FindSectionCatalog(settings.VsaCatalogSecXmlPath);
                if (!string.IsNullOrWhiteSpace(fromDir))
                    return fromDir;
            }
        }

        var env = getEnv(SectionCatalogPathEnvVar);
        if (IsCanonicalVsa2019Catalog(env, Vsa2019CatalogResolver.SectionCatalogFileName))
            return env;

        var envRoot = getEnv(SectionCatalogRootEnvVar);
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
        {
            var fromRoot = Vsa2019CatalogResolver.FindSectionCatalog(envRoot);
            if (!string.IsNullOrWhiteSpace(fromRoot))
                return fromRoot;
        }

        if (!string.IsNullOrWhiteSpace(settings.WinCanCatalogDirectory))
        {
            var fromWinCan = Vsa2019CatalogResolver.FindSectionCatalog(settings.WinCanCatalogDirectory);
            if (!string.IsNullOrWhiteSpace(fromWinCan))
                return fromWinCan;
        }

        foreach (var root in Vsa2019CatalogResolver.GetDefaultCatalogRoots(lastProjectPath: settings.LastProjectPath))
        {
            var fromCommon = Vsa2019CatalogResolver.FindSectionCatalog(root);
            if (!string.IsNullOrWhiteSpace(fromCommon))
                return fromCommon;
        }

        return null;
    }

    private static string? ResolveNodeCatalogPath(AppSettings settings, Func<string, string?> getEnv)
    {
        if (!string.IsNullOrWhiteSpace(settings.VsaCatalogNodXmlPath))
        {
            if (IsCanonicalVsa2019Catalog(settings.VsaCatalogNodXmlPath, Vsa2019CatalogResolver.NodeCatalogFileName))
                return settings.VsaCatalogNodXmlPath;

            if (Directory.Exists(settings.VsaCatalogNodXmlPath))
            {
                var fromDir = Vsa2019CatalogResolver.FindNodeCatalog(settings.VsaCatalogNodXmlPath);
                if (!string.IsNullOrWhiteSpace(fromDir))
                    return fromDir;
            }
        }

        var env = getEnv(NodeCatalogPathEnvVar);
        if (IsCanonicalVsa2019Catalog(env, Vsa2019CatalogResolver.NodeCatalogFileName))
            return env;

        var envRoot = getEnv(NodeCatalogRootEnvVar);
        if (!string.IsNullOrWhiteSpace(envRoot) && Directory.Exists(envRoot))
        {
            var fromRoot = Vsa2019CatalogResolver.FindNodeCatalog(envRoot);
            if (!string.IsNullOrWhiteSpace(fromRoot))
                return fromRoot;
        }

        if (!string.IsNullOrWhiteSpace(settings.WinCanCatalogDirectory))
        {
            var fromWinCan = Vsa2019CatalogResolver.FindNodeCatalog(settings.WinCanCatalogDirectory);
            if (!string.IsNullOrWhiteSpace(fromWinCan))
                return fromWinCan;
        }

        foreach (var root in Vsa2019CatalogResolver.GetDefaultCatalogRoots(lastProjectPath: settings.LastProjectPath))
        {
            var fromCommon = Vsa2019CatalogResolver.FindNodeCatalog(root);
            if (!string.IsNullOrWhiteSpace(fromCommon))
                return fromCommon;
        }

        return null;
    }

    private static string? ResolveKekManifestPath(string baseDirectory, Func<string, string?> getEnv)
    {
        var env = getEnv(KekManifestPathEnvVar);
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var fromData = Path.Combine(baseDirectory, "Data", KekManifestFileName);
        return File.Exists(fromData) ? fromData : null;
    }

    private static string? FindTextCatalogInRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        return Vsa2019CatalogResolver.FindSectionCatalog(root);
    }

    private static bool IsCanonicalVsa2019Catalog(string? path, string fileName)
        => !string.IsNullOrWhiteSpace(path)
           && File.Exists(path)
           && string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
