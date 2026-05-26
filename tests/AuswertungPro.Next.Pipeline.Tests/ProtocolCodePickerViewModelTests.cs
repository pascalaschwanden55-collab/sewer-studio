using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed class ProtocolCodePickerViewModelTests
{
    [Fact]
    public void SearchText_matches_source()
    {
        var vm = CreateViewModel();

        vm.SearchText = "wincan";

        var codes = Flatten(vm.CodeTree)
            .Where(n => n.Code is not null)
            .Select(n => n.Code!.Code)
            .ToList();

        Assert.Equal(new[] { "WZZ" }, codes);
    }

    [Fact]
    public void Non_selectable_and_observed_codes_are_shown_only_in_locked_tree()
    {
        var vm = CreateViewModel();

        Assert.DoesNotContain(Flatten(vm.CodeTree), n => n.Code?.Code == "BCCYY");
        Assert.DoesNotContain(Flatten(vm.CodeTree), n => n.Code?.Code == "BAG");

        var lockedCodes = Flatten(vm.LockedCodeTree)
            .Where(n => n.Code is not null)
            .Select(n => n.Code!.Code)
            .OrderBy(c => c)
            .ToList();

        Assert.Equal(new[] { "BAG", "BCCYY" }, lockedCodes);
        Assert.All(Flatten(vm.LockedCodeTree).Where(n => n.Code is not null), n => Assert.False(n.IsSelectable));
    }

    [Fact]
    public void ApplySelection_rejects_non_selectable_code_even_when_set_directly()
    {
        var vm = CreateViewModel();
        vm.SelectedCode = vm.Codes.Single(c => c.Code == "BCCYY");

        var applied = vm.ApplySelection();

        Assert.False(applied);
        Assert.Contains("nicht auswaehlbar", vm.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tree_nodes_expose_catalog_source_badges()
    {
        var vm = CreateViewModel();

        var bab = Flatten(vm.CodeTree).Single(n => n.Code?.Code == "BAB");
        var wzz = Flatten(vm.CodeTree).Single(n => n.Code?.Code == "WZZ");
        var baga = Flatten(vm.CodeTree).Single(n => n.Code?.Code == "BAGA");

        Assert.Equal("ICM", bab.SourceBadgeText);
        Assert.Equal("WinCan", wzz.SourceBadgeText);
        Assert.Equal(string.Empty, baga.SourceBadgeText);
    }

    [Fact]
    public void ApplySelection_writes_catalog_metadata_to_protocol_entry()
    {
        var entry = new ProtocolEntry();
        var vm = CreateViewModel(entry);
        vm.SelectedCode = vm.Codes.Single(c => c.Code == "BDBA");

        var applied = vm.ApplySelection();

        Assert.True(applied);
        Assert.Equal("BDBA", entry.Code);
        Assert.NotNull(entry.CodeMeta);
        Assert.Equal("BDBA", entry.CodeMeta!.Code);
        Assert.Equal(IkasCatalogSources.IkasIli, entry.CodeMeta.Parameters["catalog.source"]);
        Assert.Equal("BDB", entry.CodeMeta.Parameters["catalog.canonicalCode"]);
        Assert.Equal("A", entry.CodeMeta.Parameters["catalog.standardAnnotation"]);
    }

    private static ProtocolCodePickerViewModel CreateViewModel()
        => CreateViewModel(new ProtocolEntry());

    private static ProtocolCodePickerViewModel CreateViewModel(ProtocolEntry entry)
        => new(new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition
            {
                Code = "BAGA",
                Title = "Anschluss einragend",
                CanonicalCode = "BAG",
                Source = IkasCatalogSources.IkasIli,
                IsSelectable = true,
                Group = "Kanal/Anschluesse"
            },
            new CodeDefinition
            {
                Code = "BDBA",
                Title = "Wasserstand Standard A",
                CanonicalCode = "BDB",
                Source = IkasCatalogSources.IkasIli,
                StandardAnnotation = "A",
                IsSelectable = true,
                Group = "Kanal/Anmerkung"
            },
            new CodeDefinition
            {
                Code = "BAB",
                Title = "Verformung",
                Source = IkasCatalogSources.IkasIcm,
                IsSelectable = true,
                Group = "Kanal/Form"
            },
            new CodeDefinition
            {
                Code = "BAG",
                Title = "Anschluss einragend Regelcode",
                Source = IkasCatalogSources.IkasIcm,
                IsSelectable = false,
                Group = "Kanal/Anschluesse"
            },
            new CodeDefinition
            {
                Code = "BCCYY",
                Title = "IKAS beobachtete Erweiterung",
                Source = IkasCatalogSources.IkasXtfObserved,
                IsObservedExtension = true,
                IsSelectable = false,
                Group = "Kanal/Beobachtet"
            },
            new CodeDefinition
            {
                Code = "WZZ",
                Title = "Alter WinCan-Code",
                Source = IkasCatalogSources.WinCanFallback,
                IsSelectable = true,
                Group = "WinCan/Legacy"
            }
        }), new ProtocolEntryVM(entry));

    private static IEnumerable<CodeTreeNode> Flatten(IEnumerable<CodeTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
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
}
