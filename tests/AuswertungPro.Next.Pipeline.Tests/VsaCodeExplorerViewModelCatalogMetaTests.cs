using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class VsaCodeExplorerViewModelCatalogMetaTests
{
    [Fact]
    public void BuildProtocolEntry_writes_catalog_metadata_from_vsa_tree()
    {
        var snapshot = VsaTreeSnapshot.Capture();
        try
        {
            VsaCodeTreeCatalogAdapter.Apply(new InMemoryCodeCatalogProvider(new[]
            {
                new CodeDefinition
                {
                    Code = "BDBA",
                    Title = "Wasserstand Standard A",
                    Source = IkasCatalogSources.IkasIli,
                    CanonicalCode = "BDB",
                    StandardAnnotation = "A",
                    IsSelectable = true,
                    CategoryPath = ["Kanal"]
                }
            }));

            var vm = new VsaCodeExplorerViewModel();
            vm.SelectGroup("BD");
            vm.SelectCode("BDBA");
            vm.MeterStart = "1.00";

            var entry = vm.BuildProtocolEntry();

            Assert.Equal("BDBA", entry.Code);
            Assert.NotNull(entry.CodeMeta);
            Assert.Equal("BDBA", entry.CodeMeta!.Code);
            Assert.Equal(IkasCatalogSources.IkasIli, entry.CodeMeta.Parameters["catalog.source"]);
            Assert.Equal("BDB", entry.CodeMeta.Parameters["catalog.canonicalCode"]);
            Assert.Equal("A", entry.CodeMeta.Parameters["catalog.standardAnnotation"]);
        }
        finally
        {
            snapshot.Restore();
        }
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

    private sealed class VsaTreeSnapshot
    {
        private readonly Dictionary<string, GroupDef> _groups;
        private readonly Dictionary<string, QuantRule> _quantRules;
        private readonly Dictionary<string, ClockRule> _clockRules;
        private readonly Dictionary<string, string> _catalogLabels;

        private VsaTreeSnapshot()
        {
            _groups = new Dictionary<string, GroupDef>(VsaCodeTree.Groups, StringComparer.OrdinalIgnoreCase);
            _quantRules = new Dictionary<string, QuantRule>(VsaCodeTree.QuantRules, StringComparer.OrdinalIgnoreCase);
            _clockRules = new Dictionary<string, ClockRule>(VsaCodeTree.ClockRules, StringComparer.OrdinalIgnoreCase);
            _catalogLabels = new Dictionary<string, string>(VsaCodeTree.CatalogLabels, StringComparer.OrdinalIgnoreCase);
        }

        public static VsaTreeSnapshot Capture() => new();

        public void Restore()
        {
            Replace(VsaCodeTree.Groups, _groups);
            Replace(VsaCodeTree.QuantRules, _quantRules);
            Replace(VsaCodeTree.ClockRules, _clockRules);
            Replace(VsaCodeTree.CatalogLabels, _catalogLabels);
        }

        private static void Replace<TKey, TValue>(
            Dictionary<TKey, TValue> target,
            Dictionary<TKey, TValue> source)
            where TKey : notnull
        {
            target.Clear();
            foreach (var item in source)
                target[item.Key] = item.Value;
        }
    }
}
