using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class VsaCodeExplorerViewModelCatalogMetaTests
{
    [Fact]
    public void Constructor_without_catalog_does_not_load_legacy_static_tree()
    {
        var vm = new VsaCodeExplorerViewModel();

        Assert.Empty(vm.CurrentTiles);
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public void BuildProtocolEntry_writes_catalog_metadata_from_selection_catalog()
    {
        var catalog = new CodeCatalogSelectionCatalog(new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition
            {
                Code = "BDBA",
                Title = "Wasserstand Standard A",
                Source = VsaKekCatalogSources.Ili,
                CanonicalCode = "BDB",
                StandardAnnotation = "A",
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            }
        }));

        var vm = new VsaCodeExplorerViewModel(catalog: catalog);
        vm.SelectGroup("BD");
        vm.SelectCode("BDBA");
        vm.MeterStart = "1.00";

        var entry = vm.BuildProtocolEntry();

        Assert.Equal("BDBA", entry.Code);
        Assert.NotNull(entry.CodeMeta);
        Assert.Equal("BDBA", entry.CodeMeta!.Code);
        Assert.Equal(VsaKekCatalogSources.Ili, entry.CodeMeta.Parameters["catalog.source"]);
        Assert.Equal("BDB", entry.CodeMeta.Parameters["catalog.canonicalCode"]);
        Assert.Equal("A", entry.CodeMeta.Parameters["catalog.standardAnnotation"]);
    }

    private sealed class InMemoryCodeCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _codes;

        public InMemoryCodeCatalogProvider(IReadOnlyList<CodeDefinition> codes)
            => _codes = codes;

        public IReadOnlyList<CodeDefinition> GetAll() => _codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => _codes.Where(c => c.IsSelectable && !c.IsObservedExtension).Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
