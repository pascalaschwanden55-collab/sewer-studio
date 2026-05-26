using AuswertungPro.Next.Application.Protocol;
namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaKekCatalogBuilderTests
{
    private const string VsaKekArchivePath =
        @"D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export\Bin\Bin.7z";

    private const string VsaKekXtfPath =
        @"D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export\Erstfeld_Jagdmatt_38454_0426.xtf";

    [Fact]
    public void BuildFromVsaKekArchive_ProducesExpectedManifestRules()
    {
        if (!File.Exists(VsaKekArchivePath) || !File.Exists(VsaKekXtfPath))
            return;

        var manifest = BuildManifestFromVsaKekExport();

        Assert.Equal(322, manifest.Codes.Count(c =>
            string.Equals(c.Source, VsaKekCatalogSources.Ili, StringComparison.OrdinalIgnoreCase)
            && c.CategoryPath.Contains("Kanal", StringComparer.OrdinalIgnoreCase)));
        Assert.Equal(VsaKekCatalogSources.Icm, RequireCode(manifest, "BAG").Source);

        var baga = RequireCode(manifest, "BAGA");
        Assert.Equal("BAG", baga.CanonicalCode);
        AssertRequiredNumberParameter(baga, "Q1");

        foreach (var code in new[] { "BDBA", "BDBD", "BDBF", "BDBK" })
        {
            var def = RequireCode(manifest, code);
            Assert.Equal("BDB", def.CanonicalCode);
            Assert.DoesNotContain(def.Parameters, p => string.Equals(p.DataKey, "Q1", StringComparison.OrdinalIgnoreCase));
        }

        Assert.Equal("A", RequireCode(manifest, "BDBA").StandardAnnotation);
        Assert.Equal("D", RequireCode(manifest, "BDBD").StandardAnnotation);

        var bccyy = RequireCode(manifest, "BCCYY");
        Assert.Equal(VsaKekCatalogSources.XtfObserved, bccyy.Source);
        Assert.True(bccyy.IsObservedExtension);
        Assert.False(bccyy.IsSelectable);

        var provider = new CompositeCodeCatalogProvider(new ICodeCatalogProvider[]
        {
            new InMemoryCodeCatalogProvider(manifest.Codes)
        });
        Assert.Contains("BAGA", provider.AllowedCodes());
        Assert.DoesNotContain("BAG", provider.AllowedCodes());
        Assert.DoesNotContain("BCCYY", provider.AllowedCodes());
        Assert.True(provider.TryGet("BCCYY", out _));

        var selectionCatalog = new CodeCatalogSelectionCatalog(provider);
        Assert.Equal("Anschluss einragend", selectionCatalog.Groups["BA"].Codes["BAGA"].Label);
        Assert.True(selectionCatalog.Groups["BA"].Codes.ContainsKey("BAGA"));
        Assert.DoesNotContain(selectionCatalog.Groups.Values.SelectMany(g => g.Codes.Keys),
            code => string.Equals(code, "BCCYY", StringComparison.OrdinalIgnoreCase));
        var (treeQ1, treeQ2) = selectionCatalog.GetQuantRule("BAGA", null);
        Assert.NotNull(treeQ1);
        Assert.Equal("P", treeQ1!.Pflicht);
        Assert.Null(treeQ2);

        foreach (var code in new[] { "BAB", "BAC", "BAG", "BAI", "BAJ", "BBA", "BBB", "BBC", "BCA", "BDD" })
        {
            AssertRequiredNumberParameter(RequireCode(manifest, code), "Q1");
        }

        var bca = RequireCode(manifest, "BCA");
        AssertRequiredNumberParameter(bca, "Q1");
        AssertOptionalNumberParameter(bca, "Q2");

        foreach (var code in new[] { "DCA", "DCG" })
        {
            var def = RequireCode(manifest, code);
            AssertRequiredNumberParameter(def, "Q1");
            AssertOptionalNumberParameter(def, "Q2");
        }
    }

    [Fact]
    public void CompositeProvider_KeepsVsaKekFirstAndMarksWinCanFallback()
    {
        var vsaKek = new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition
            {
                Code = "BAGA",
                Title = "VSA-KEK BAG",
                Source = VsaKekCatalogSources.Ili,
                CanonicalCode = "BAG"
            }
        });
        var wincan = new SourceDecoratingCodeCatalogProvider(
            new InMemoryCodeCatalogProvider(new[]
            {
                new CodeDefinition { Code = "BAGA", Title = "WinCan BAG" },
                new CodeDefinition { Code = "ZZZ", Title = "WinCan only" }
            }),
            VsaKekCatalogSources.WinCanFallback);

        var provider = new CompositeCodeCatalogProvider(new ICodeCatalogProvider[] { vsaKek, wincan });

        Assert.True(provider.TryGet("BAGA", out var baga));
        Assert.Equal("VSA-KEK BAG", baga.Title);
        Assert.Equal(VsaKekCatalogSources.Ili, baga.Source);

        Assert.True(provider.TryGet("ZZZ", out var fallback));
        Assert.Equal(VsaKekCatalogSources.WinCanFallback, fallback.Source);
    }

    private static CodeCatalogDocument BuildManifestFromVsaKekExport()
    {
        var ili = VsaKekCatalogArchiveReader.ReadTextEntry(VsaKekArchivePath, VsaKekCatalogArchiveReader.IliEntryName);
        var sectionIcm = VsaKekCatalogArchiveReader.ReadTextEntry(VsaKekArchivePath, VsaKekCatalogArchiveReader.SectionIcmEntryName);
        var manholeIcm = VsaKekCatalogArchiveReader.ReadTextEntry(VsaKekArchivePath, VsaKekCatalogArchiveReader.ManholeIcmEntryName);
        var xtf = File.ReadAllText(VsaKekXtfPath);

        return VsaKekCatalogBuilder.Build(ili, sectionIcm, manholeIcm, new[] { xtf });
    }

    private static CodeDefinition RequireCode(CodeCatalogDocument manifest, string code)
        => manifest.Codes.Single(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    private static void AssertRequiredNumberParameter(CodeDefinition def, string dataKey)
    {
        var parameter = Assert.Single(def.Parameters, p => string.Equals(p.DataKey, dataKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("number", parameter.Type);
        Assert.True(parameter.Required);
    }

    private static void AssertOptionalNumberParameter(CodeDefinition def, string dataKey)
    {
        var parameter = Assert.Single(def.Parameters, p => string.Equals(p.DataKey, dataKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("number", parameter.Type);
        Assert.False(parameter.Required);
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
}
