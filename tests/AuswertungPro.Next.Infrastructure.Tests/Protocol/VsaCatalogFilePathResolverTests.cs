using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests.Protocol;

public sealed class VsaCatalogFilePathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "VsaCatalogFilePathResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_verwendet_Umgebungsordner_und_Manifest_aus_dem_App_Datenordner()
    {
        var catalogRoot = Path.Combine(_root, "Catalogs");
        var baseDirectory = Path.Combine(_root, "App");
        Directory.CreateDirectory(catalogRoot);
        Directory.CreateDirectory(Path.Combine(baseDirectory, "Data"));

        var sectionPath = Path.Combine(catalogRoot, Vsa2019CatalogResolver.SectionCatalogFileName);
        var nodePath = Path.Combine(catalogRoot, Vsa2019CatalogResolver.NodeCatalogFileName);
        var manifestPath = Path.Combine(baseDirectory, "Data", VsaCatalogPathNames.KekManifestFileName);
        File.WriteAllText(sectionPath, "<sec />");
        File.WriteAllText(nodePath, "<nod />");
        File.WriteAllText(manifestPath, "{}");

        var environment = new Dictionary<string, string?>
        {
            [VsaCatalogPathNames.SectionCatalogRootEnvironmentVariable] = catalogRoot,
            [VsaCatalogPathNames.NodeCatalogRootEnvironmentVariable] = catalogRoot
        };
        IVsaCatalogPathResolver resolver = new VsaCatalogFilePathResolver();

        var result = resolver.Resolve(new VsaCatalogPathRequest(
            SectionCatalogPath: null,
            NodeCatalogPath: null,
            WinCanCatalogDirectory: null,
            LastProjectPath: null,
            BaseDirectory: baseDirectory,
            EnvironmentVariableReader: name => environment.GetValueOrDefault(name)));

        Assert.Equal(sectionPath, result.SectionCatalogPath);
        Assert.Equal(nodePath, result.NodeCatalogPath);
        Assert.Equal(manifestPath, result.KekManifestPath);
        Assert.Equal(new[] { manifestPath, sectionPath, nodePath }, result.SourcePaths);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
