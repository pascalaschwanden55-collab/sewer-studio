namespace AuswertungPro.Next.Application.Protocol;

public static class Vsa2019CatalogResolver
{
    public const string SectionCatalogFileName = "EN13508_VSA-2019_CH_DEU_SEC.xml";
    public const string NodeCatalogFileName = "EN13508_VSA-2019_CH_DEU_NOD.xml";

    public static string? FindSectionCatalog(string? root)
        => FindCatalog(root, SectionCatalogFileName);

    public static string? FindNodeCatalog(string? root)
        => FindCatalog(root, NodeCatalogFileName);

    public static string GetPublicDocumentsCatalogRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
            "CDLAB",
            "Common",
            "Catalogs");

    public static IReadOnlyList<string> GetDefaultCatalogRoots(string? winCanCatalogDir = null, string? lastProjectPath = null)
    {
        var roots = new List<string>();

        AddIfExists(roots, winCanCatalogDir);
        AddIfExists(roots, GetPublicDocumentsCatalogRoot());

        AddIfExists(roots, @"C:\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs");
        AddIfExists(roots, @"C:\Program Files\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs");
        AddIfExists(roots, @"C:\Program Files (x86)\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs");

        var programDataCatalogs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CDLAB",
            "Common",
            "Catalogs");
        AddIfExists(roots, programDataCatalogs);

        if (!string.IsNullOrWhiteSpace(lastProjectPath))
        {
            var projectCatalogPath = Path.Combine(
                lastProjectPath,
                "DISK1",
                "System",
                "ProgramData",
                "CDLAB",
                "Common",
                "Catalogs");
            AddIfExists(roots, projectCatalogPath);
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? FindCatalog(string? root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        var candidate = Path.Combine(root, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static void AddIfExists(List<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            roots.Add(path);
    }
}
