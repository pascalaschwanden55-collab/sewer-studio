using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed class ObservationCatalogViewModelCatalogLabelTests
{
    [Fact]
    public void Subcategory_labels_are_resolved_from_catalog_provider()
    {
        var vm = CreateViewModel();

        SelectRoot(vm, "BA");

        Assert.Contains(vm.Columns[1].Items, item =>
            item.Node?.Key == "BAB" && item.Label == "BAB  Riss aus Katalog");
        Assert.Contains(vm.Columns[1].Items, item =>
            item.Node?.Key == "BAH" && item.Label == "BAH  Versatz");

        SelectRoot(vm, "BB");

        Assert.Contains(vm.Columns[1].Items, item =>
            item.Node?.Key == "BBA" && item.Label == "BBA  Wurzeln");
    }

    [Fact]
    public void Unknown_subcategory_label_falls_back_to_code_prefix()
    {
        var vm = CreateViewModel();

        SelectRoot(vm, "ZZ");

        Assert.Contains(vm.Columns[1].Items, item =>
            item.Node?.Key == "ZZZ" && item.Label == "ZZZ");
    }

    private static ObservationCatalogViewModel CreateViewModel()
        => new(new LookupBackedCatalogProvider(
            visibleCodes: new[]
            {
                new CodeDefinition { Code = "BABAC", Title = "Laengsriss", Group = "Kanal" },
                new CodeDefinition { Code = "BAHC", Title = "Muffenversatz", Group = "Kanal" },
                new CodeDefinition { Code = "BBAA", Title = "Pfahlwurzel", Group = "Kanal" },
                new CodeDefinition { Code = "ZZZAA", Title = string.Empty, Group = "Unbekannt" }
            },
            lookupOnlyCodes: new[]
            {
                new CodeDefinition { Code = "BAB", Title = "Riss aus Katalog" },
                new CodeDefinition { Code = "BAH", Title = "Versatz" },
                new CodeDefinition { Code = "BBA", Title = "Wurzeln" }
            }), new ProtocolEntry());

    private static void SelectRoot(ObservationCatalogViewModel vm, string key)
    {
        var item = vm.Columns[0].Items.Single(x =>
            string.Equals(x.Node?.Key, key, StringComparison.OrdinalIgnoreCase));
        vm.SelectColumnItem(0, item);
    }

    private sealed class LookupBackedCatalogProvider : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _visibleCodes;
        private readonly Dictionary<string, CodeDefinition> _lookup;

        public LookupBackedCatalogProvider(
            IReadOnlyList<CodeDefinition> visibleCodes,
            IReadOnlyList<CodeDefinition> lookupOnlyCodes)
        {
            _visibleCodes = visibleCodes;
            _lookup = visibleCodes
                .Concat(lookupOnlyCodes)
                .Where(c => !string.IsNullOrWhiteSpace(c.Code))
                .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<CodeDefinition> GetAll() => _visibleCodes;

        public bool TryGet(string code, out CodeDefinition def)
            => _lookup.TryGetValue(code, out def!);

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes()
            => _visibleCodes.Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
