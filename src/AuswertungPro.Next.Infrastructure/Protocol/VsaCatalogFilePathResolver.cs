using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Protocol;

public sealed class VsaCatalogFilePathResolver : IVsaCatalogPathResolver
{
    public VsaCatalogPathResult Resolve(VsaCatalogPathRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var getEnvironmentVariable = request.EnvironmentVariableReader
                                     ?? Environment.GetEnvironmentVariable;
        var sectionCatalogPath = ResolveSectionCatalogPath(request, getEnvironmentVariable);
        var nodeCatalogPath = ResolveNodeCatalogPath(request, getEnvironmentVariable);
        var kekManifestPath = ResolveKekManifestPath(
            request.BaseDirectory ?? AppContext.BaseDirectory,
            getEnvironmentVariable);

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

        return new VsaCatalogPathResult(
            sectionCatalogPath,
            nodeCatalogPath,
            kekManifestPath,
            xmlCatalogPaths,
            sourcePaths,
            sourcePaths.Length > 0 ? string.Join(" | ", sourcePaths) : null);
    }

    public string? ResolveTextFallbackPath(
        string? configuredSectionCatalogPath,
        string? resolvedXmlPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredSectionCatalogPath))
        {
            if (File.Exists(configuredSectionCatalogPath))
            {
                var directory = Path.GetDirectoryName(configuredSectionCatalogPath);
                var fromDirectory = FindCatalog(directory, Vsa2019CatalogResolver.SectionCatalogFileName);
                if (!string.IsNullOrWhiteSpace(fromDirectory))
                    return fromDirectory;
            }
            else if (Directory.Exists(configuredSectionCatalogPath))
            {
                var fromRoot = FindCatalog(
                    configuredSectionCatalogPath,
                    Vsa2019CatalogResolver.SectionCatalogFileName);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                    return fromRoot;
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedXmlPath))
            return null;

        var resolvedDirectory = Path.GetDirectoryName(resolvedXmlPath);
        if (string.IsNullOrWhiteSpace(resolvedDirectory))
            return null;

        var parent = Directory.GetParent(resolvedDirectory);
        if (parent is null
            || !string.Equals(
                Path.GetFileName(resolvedDirectory),
                "Version4",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return FindCatalog(parent.FullName, Vsa2019CatalogResolver.SectionCatalogFileName);
    }

    private static string? ResolveSectionCatalogPath(
        VsaCatalogPathRequest request,
        Func<string, string?> getEnvironmentVariable)
    {
        var configured = ResolveConfiguredCatalog(
            request.SectionCatalogPath,
            Vsa2019CatalogResolver.SectionCatalogFileName);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var environmentFile = getEnvironmentVariable(
            VsaCatalogPathNames.SectionCatalogPathEnvironmentVariable);
        if (IsCanonicalCatalog(environmentFile, Vsa2019CatalogResolver.SectionCatalogFileName))
            return environmentFile;

        var environmentRoot = getEnvironmentVariable(
            VsaCatalogPathNames.SectionCatalogRootEnvironmentVariable);
        var fromEnvironmentRoot = FindCatalog(
            environmentRoot,
            Vsa2019CatalogResolver.SectionCatalogFileName);
        if (!string.IsNullOrWhiteSpace(fromEnvironmentRoot))
            return fromEnvironmentRoot;

        var fromWinCan = FindCatalog(
            request.WinCanCatalogDirectory,
            Vsa2019CatalogResolver.SectionCatalogFileName);
        if (!string.IsNullOrWhiteSpace(fromWinCan))
            return fromWinCan;

        return FindFirstCatalog(
            GetDefaultCatalogRoots(request.LastProjectPath),
            Vsa2019CatalogResolver.SectionCatalogFileName);
    }

    private static string? ResolveNodeCatalogPath(
        VsaCatalogPathRequest request,
        Func<string, string?> getEnvironmentVariable)
    {
        var configured = ResolveConfiguredCatalog(
            request.NodeCatalogPath,
            Vsa2019CatalogResolver.NodeCatalogFileName);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var environmentFile = getEnvironmentVariable(
            VsaCatalogPathNames.NodeCatalogPathEnvironmentVariable);
        if (IsCanonicalCatalog(environmentFile, Vsa2019CatalogResolver.NodeCatalogFileName))
            return environmentFile;

        var environmentRoot = getEnvironmentVariable(
            VsaCatalogPathNames.NodeCatalogRootEnvironmentVariable);
        var fromEnvironmentRoot = FindCatalog(
            environmentRoot,
            Vsa2019CatalogResolver.NodeCatalogFileName);
        if (!string.IsNullOrWhiteSpace(fromEnvironmentRoot))
            return fromEnvironmentRoot;

        var fromWinCan = FindCatalog(
            request.WinCanCatalogDirectory,
            Vsa2019CatalogResolver.NodeCatalogFileName);
        if (!string.IsNullOrWhiteSpace(fromWinCan))
            return fromWinCan;

        return FindFirstCatalog(
            GetDefaultCatalogRoots(request.LastProjectPath),
            Vsa2019CatalogResolver.NodeCatalogFileName);
    }

    private static string? ResolveConfiguredCatalog(string? configuredPath, string fileName)
    {
        if (IsCanonicalCatalog(configuredPath, fileName))
            return configuredPath;

        return FindCatalog(configuredPath, fileName);
    }

    private static string? ResolveKekManifestPath(
        string baseDirectory,
        Func<string, string?> getEnvironmentVariable)
    {
        var environmentPath = getEnvironmentVariable(
            VsaCatalogPathNames.KekManifestPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath) && File.Exists(environmentPath))
            return environmentPath;

        var fromDataDirectory = Path.Combine(
            baseDirectory,
            "Data",
            VsaCatalogPathNames.KekManifestFileName);
        return File.Exists(fromDataDirectory) ? fromDataDirectory : null;
    }

    private static string? FindFirstCatalog(IEnumerable<string> roots, string fileName)
    {
        foreach (var root in roots)
        {
            var path = FindCatalog(root, fileName);
            if (!string.IsNullOrWhiteSpace(path))
                return path;
        }

        return null;
    }

    private static string? FindCatalog(string? root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        var candidate = Path.Combine(root, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static bool IsCanonicalCatalog(string? path, string fileName)
        => !string.IsNullOrWhiteSpace(path)
           && File.Exists(path)
           && string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> GetDefaultCatalogRoots(string? lastProjectPath)
    {
        var roots = new List<string>();
        AddIfExists(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "CDLAB",
            "Common",
            "Catalogs"));
        AddIfExists(roots, @"C:\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs");
        AddIfExists(roots, @"C:\Program Files\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs");
        AddIfExists(roots, @"C:\Program Files (x86)\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs");
        AddIfExists(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CDLAB",
            "Common",
            "Catalogs"));

        if (!string.IsNullOrWhiteSpace(lastProjectPath))
        {
            AddIfExists(roots, Path.Combine(
                lastProjectPath,
                "DISK1",
                "System",
                "ProgramData",
                "CDLAB",
                "Common",
                "Catalogs"));
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddIfExists(ICollection<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            roots.Add(path);
    }
}
