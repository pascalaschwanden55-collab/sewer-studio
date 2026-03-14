using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Metadata for a single WinCanVX catalog XML file.
/// </summary>
public sealed class WinCanCatalogInfo
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Standard { get; set; } = "";
    public string CustomType { get; set; } = "";
    public string Country { get; set; } = "";
    public string Language { get; set; } = "";
    public string ObjectType { get; set; } = "";
    public string? Version { get; set; }
    public string? Description { get; set; }

    /// <summary>Display label for the catalog list.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(CustomType)
            ? $"{Standard} ({Country}-{Language}) [{ObjectType}]"
            : $"{Standard} {CustomType} ({Country}-{Language}) [{ObjectType}]";
}

/// <summary>
/// Discovers and reads metadata from WinCanVX catalog XML files (WCCat format).
/// </summary>
public sealed class WinCanCatalogDiscoveryService
{
    private static readonly XNamespace WcNs = "CDLAB.WinCan.WinCanCatalog_2011-04-04_2";

    /// <summary>
    /// Scans a single directory for WCCat XML catalogs.
    /// </summary>
    public IReadOnlyList<WinCanCatalogInfo> DiscoverCatalogs(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return Array.Empty<WinCanCatalogInfo>();

        var results = new List<WinCanCatalogInfo>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*.xml", SearchOption.TopDirectoryOnly))
            {
                var info = ReadCatalogMetadata(file);
                if (info is not null)
                    results.Add(info);
            }

            // Also check Version4 subdirectory (common WinCanVX layout)
            var v4 = Path.Combine(directoryPath, "Version4");
            if (Directory.Exists(v4))
            {
                foreach (var file in Directory.EnumerateFiles(v4, "*.xml", SearchOption.TopDirectoryOnly))
                {
                    var info = ReadCatalogMetadata(file);
                    if (info is not null)
                        results.Add(info);
                }
            }
        }
        catch
        {
            // Ignore I/O errors during discovery
        }

        return results
            .OrderBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Standard, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.CustomType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Scans multiple directories for WCCat XML catalogs, deduplicating by file name.
    /// </summary>
    public IReadOnlyList<WinCanCatalogInfo> DiscoverCatalogs(IEnumerable<string> directories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<WinCanCatalogInfo>();

        foreach (var dir in directories)
        {
            foreach (var info in DiscoverCatalogs(dir))
            {
                if (seen.Add(info.FilePath))
                    all.Add(info);
            }
        }

        return all
            .OrderBy(c => c.Country, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Standard, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.CustomType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Reads only the CATALOG metadata from a WCCat XML file (fast, does not parse codes).
    /// Returns null if the file is not a WCCat catalog.
    /// </summary>
    public WinCanCatalogInfo? ReadCatalogMetadata(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            // Use streaming to read only the first elements
            using var stream = File.OpenRead(filePath);
            using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Ignore,
                IgnoreWhitespace = true
            });

            // Find root element
            if (!reader.Read() || !reader.IsStartElement())
                return null;

            // Check if it is WCCat format
            if (!string.Equals(reader.LocalName, "WCCat", StringComparison.OrdinalIgnoreCase))
                return null;

            // Read the document just far enough to find the CATALOG element
            var doc = XDocument.Load(filePath);
            var root = doc.Root;
            if (root is null)
                return null;

            var ns = root.Name.Namespace;
            var cat = root.Element(ns + "CATALOG");
            if (cat is null)
                return null;

            return new WinCanCatalogInfo
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Standard = (cat.Element(ns + "CAT_BaseType")?.Value ?? "").Trim(),
                CustomType = (cat.Element(ns + "CAT_CustomType")?.Value ?? "").Trim(),
                Country = (cat.Element(ns + "CAT_Country")?.Value ?? "").Trim(),
                Language = (cat.Element(ns + "CAT_Language")?.Value ?? "").Trim(),
                ObjectType = (cat.Element(ns + "CAT_ObjectType")?.Value ?? "").Trim(),
                Version = cat.Element(ns + "CAT_Version")?.Value?.Trim(),
                Description = cat.Element(ns + "CAT_Description")?.Value?.Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns common directories where WinCanVX catalogs might be found.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultSearchDirectories(string? winCanCatalogDir = null, string? lastProjectPath = null)
    {
        var dirs = new List<string>();

        // 1. User-configured catalog directory
        if (!string.IsNullOrWhiteSpace(winCanCatalogDir) && Directory.Exists(winCanCatalogDir))
            dirs.Add(winCanCatalogDir);

        // 2. Common WinCanVX installation paths
        var commonPaths = new[]
        {
            @"C:\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
            @"C:\Program Files\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
            @"C:\Program Files (x86)\CDLAB\WinCanVX\WinCanMerger\App_Data\Catalogs",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CDLAB", "Common", "Catalogs")
        };

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
                dirs.Add(path);
        }

        // 3. Project-embedded catalogs
        if (!string.IsNullOrWhiteSpace(lastProjectPath))
        {
            var projectCatalogPath = Path.Combine(lastProjectPath, "DISK1", "System", "ProgramData", "CDLAB", "Common", "Catalogs");
            if (Directory.Exists(projectCatalogPath))
                dirs.Add(projectCatalogPath);
        }

        return dirs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
