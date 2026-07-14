namespace AuswertungPro.Next.Application.Protocol;

public static class VsaCatalogPathNames
{
    public const string SectionCatalogPathEnvironmentVariable = "VSA_CATALOG_SEC_XML";
    public const string SectionCatalogRootEnvironmentVariable = "VSA_CATALOG_ROOT";
    public const string NodeCatalogPathEnvironmentVariable = "VSA_CATALOG_NOD_XML";
    public const string NodeCatalogRootEnvironmentVariable = "VSA_CATALOG_NOD_ROOT";
    public const string KekManifestPathEnvironmentVariable = "VSA_KEK_2020_CATALOG_MANIFEST";
    public const string KekManifestFileName = "vsa_kek_2020_catalog_manifest.json";
}

public sealed record VsaCatalogPathRequest(
    string? SectionCatalogPath,
    string? NodeCatalogPath,
    string? WinCanCatalogDirectory,
    string? LastProjectPath,
    string? BaseDirectory = null,
    Func<string, string?>? EnvironmentVariableReader = null);

public sealed record VsaCatalogPathResult(
    string? SectionCatalogPath,
    string? NodeCatalogPath,
    string? KekManifestPath,
    IReadOnlyList<string> XmlCatalogPaths,
    IReadOnlyList<string> SourcePaths,
    string? DisplayPath);

public interface IVsaCatalogPathResolver
{
    VsaCatalogPathResult Resolve(VsaCatalogPathRequest request);

    string? ResolveTextFallbackPath(
        string? configuredSectionCatalogPath,
        string? resolvedXmlPath);
}
