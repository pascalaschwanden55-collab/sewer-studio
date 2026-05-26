using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class WinCanVsa2019CatalogTests
{
    [Fact]
    public void DefaultDiscovery_FindsConfiguredVsa2019SecAndNodCatalogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "SewerStudioVsa2019", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            WriteCatalog(root, Vsa2019CatalogResolver.SectionCatalogFileName, "SEC");
            WriteCatalog(root, Vsa2019CatalogResolver.NodeCatalogFileName, "NOD");

            var discovery = new WinCanCatalogDiscoveryService();
            var roots = WinCanCatalogDiscoveryService.GetDefaultSearchDirectories(root);
            var catalogs = discovery.DiscoverCatalogs(roots);

            Assert.Contains(catalogs, c =>
                c.FileName == Vsa2019CatalogResolver.SectionCatalogFileName &&
                c.CustomType == "VSA-2019" &&
                c.ObjectType == "SEC");
            Assert.Contains(catalogs, c =>
                c.FileName == Vsa2019CatalogResolver.NodeCatalogFileName &&
                c.CustomType == "VSA-2019" &&
                c.ObjectType == "NOD");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Resolver_UsesOnlyCanonicalRootVsa2019Catalogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "SewerStudioVsa2019", Guid.NewGuid().ToString("N"));
        var version4 = Path.Combine(root, "Version4");
        Directory.CreateDirectory(version4);

        try
        {
            File.WriteAllText(Path.Combine(root, "EN13508_VSA_CH_DEU_SEC.xml"), "<old />");
            File.WriteAllText(Path.Combine(version4, Vsa2019CatalogResolver.SectionCatalogFileName), "<v4 />");

            Assert.Null(Vsa2019CatalogResolver.FindSectionCatalog(root));

            var canonical = Path.Combine(root, Vsa2019CatalogResolver.SectionCatalogFileName);
            File.WriteAllText(canonical, "<canonical />");

            Assert.Equal(canonical, Vsa2019CatalogResolver.FindSectionCatalog(root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompositeProvider_MergesSecAndNodCodes()
    {
        var sec = new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition { Code = "BABAC", Title = "Riss Haltung", Group = "SEC" }
        });
        var nod = new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition { Code = "DAB", Title = "Schachtcode", Group = "NOD" }
        });

        var provider = new CompositeCodeCatalogProvider(new ICodeCatalogProvider[] { sec, nod });

        Assert.True(provider.TryGet("BABAC", out var secCode));
        Assert.Equal("Riss Haltung", secCode.Title);
        Assert.True(provider.TryGet("DAB", out var nodCode));
        Assert.Equal("Schachtcode", nodCode.Title);
        Assert.Contains("BABAC", provider.AllowedCodes());
        Assert.Contains("DAB", provider.AllowedCodes());
    }

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
        {
            _codes = codes;
        }

        public IReadOnlyList<CodeDefinition> GetAll() => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) => _codes = codes;

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Select(c => c.Code).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }

    private static void WriteCatalog(string root, string fileName, string objectType)
    {
        var xml = $$"""
            <WCCat xmlns="CDLAB.WinCan.WinCanCatalog_2011-04-04_2">
              <CATALOG>
                <CAT_BaseType>EN13508</CAT_BaseType>
                <CAT_CustomType>VSA-2019</CAT_CustomType>
                <CAT_Country>CH</CAT_Country>
                <CAT_Language>DEU</CAT_Language>
                <CAT_ObjectType>{{objectType}}</CAT_ObjectType>
              </CATALOG>
            </WCCat>
            """;
        File.WriteAllText(Path.Combine(root, fileName), xml);
    }
}
