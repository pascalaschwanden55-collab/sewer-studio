using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using System.IO;

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

    [Fact]
    public void Char1_tiles_show_abbreviation_and_catalog_clear_text()
    {
        var rules = new CodeCatalogSelectionCatalog(new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition
            {
                Code = "AED",
                Title = "Rohrmaterialwechsel",
                Source = VsaKekCatalogSources.Ili,
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            },
            new CodeDefinition
            {
                Code = "AEDXO",
                Title = "Rohrmaterialwechsel: Polyethylen",
                Source = VsaKekCatalogSources.Ili,
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            }
        }));
        var vm = new VsaCodeExplorerViewModel(
            catalog: new VsaCodeTreeSelectionCatalog(rules));

        vm.SelectGroup("AE");
        vm.SelectCode("AED");

        var polyethylene = Assert.Single(vm.Char1Tiles, tile => tile.Label == "AEDXO");
        Assert.Equal("PE · Polyethylen", polyethylene.Description);
    }

    [Fact]
    public void Char1_tile_keeps_curated_option_when_manifest_only_repeats_parent_title()
    {
        var rules = new CodeCatalogSelectionCatalog(new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition
            {
                Code = "AED",
                Title = "Rohrmaterialwechsel",
                Source = VsaKekCatalogSources.Ili,
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            },
            new CodeDefinition
            {
                Code = "AEDXA",
                Title = "Rohrmaterialwechsel",
                Source = VsaKekCatalogSources.Ili,
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            }
        }));
        var vm = new VsaCodeExplorerViewModel(
            catalog: new VsaCodeTreeSelectionCatalog(rules));

        vm.SelectGroup("AE");
        vm.SelectCode("AED");

        var unknown = Assert.Single(vm.Char1Tiles, tile => tile.Label == "AEDXA");
        Assert.Equal("unbek.", unknown.Description);
    }

    [Fact]
    public void Real_catalog_uses_clear_navigation_and_exact_bcc_final_text()
    {
        var vm = CreateRealCatalogViewModel();

        vm.SelectGroup("BC");
        vm.SelectCode("BCC");

        Assert.Equal(
            "Bogen nach links",
            Assert.Single(vm.Char1Tiles, tile => tile.Label == "BCCA").Description);
        Assert.Equal(
            "Bogen nach rechts",
            Assert.Single(vm.Char1Tiles, tile => tile.Label == "BCCB").Description);
        Assert.Equal(
            "Bogen vertikal",
            Assert.Single(vm.Char1Tiles, tile => tile.Label == "BCCY").Description);
        Assert.DoesNotContain(
            vm.Char1Tiles,
            tile => string.Equals(
                tile.Description,
                "Vertikale Richtung",
                StringComparison.OrdinalIgnoreCase));

        vm.SelectChar1("A");
        Assert.Equal(
            "Bogen nach links oben",
            Assert.Single(vm.Char2Tiles, tile => tile.Label == "BCCAA").Description);
        vm.SelectChar2("A");
        vm.MeterStart = "2.10";
        vm.Q1Value = "97";

        Assert.Equal("BCCAA", vm.FinalCode);
        Assert.Equal("Bogen nach links oben", vm.FinalLabel);
        Assert.Null(vm.FinalSublabel);
        Assert.Equal("\u00b0", vm.Q1Rule?.Einheit);
        Assert.Equal("none", vm.ClockMode);
        Assert.True(vm.CanConfirm);

        var entry = vm.BuildProtocolEntry();
        Assert.Equal("Bogen nach links oben", entry.Beschreibung);
        Assert.Equal(VsaKekCatalogSources.Ili, entry.CodeMeta?.Parameters["catalog.source"]);

        vm.SelectChar1("Y");
        Assert.DoesNotContain(vm.Char2Tiles, tile => tile.Label == "BCCYY");
    }

    [Fact]
    public void Real_catalog_shows_bca_clear_text_and_millimeter_units()
    {
        var vm = CreateRealCatalogViewModel();

        vm.SelectGroup("BC");
        vm.SelectCode("BCA");

        Assert.Equal(
            "Anschluss mit Formst\u00fcck",
            Assert.Single(vm.Char1Tiles, tile => tile.Label == "BCAA").Description);
        Assert.DoesNotContain(
            vm.Char1Tiles,
            tile => string.Equals(tile.Description, "Status", StringComparison.OrdinalIgnoreCase));

        vm.SelectChar1("A");
        vm.SelectChar2("A");
        vm.MeterStart = "4.40";
        vm.Q1Value = "100";

        Assert.Equal("BCAAA", vm.FinalCode);
        Assert.Equal("Anschluss mit Formst\u00fcck", vm.FinalLabel);
        Assert.Equal("mm", vm.Q1Rule?.Einheit);
        Assert.Equal("mm", vm.Q2Rule?.Einheit);
        Assert.Equal(0d, vm.Q1Rule?.Min);
        Assert.Equal(0d, vm.Q2Rule?.Min);
        Assert.True(vm.CanConfirm);
        Assert.Equal("Anschluss mit Formst\u00fcck", vm.BuildProtocolEntry().Beschreibung);
    }

    [Fact]
    public void Real_catalog_only_offers_selectable_surface_damage_combinations()
    {
        var vm = CreateRealCatalogViewModel();

        vm.SelectGroup("BA");
        vm.SelectCode("BAF");
        vm.SelectChar1("B");

        Assert.Contains(vm.Char2Tiles, tile => tile.Label == "BAFBA");
        Assert.Contains(vm.Char2Tiles, tile => tile.Label == "BAFBE");
        Assert.Contains(vm.Char2Tiles, tile => tile.Label == "BAFBZ");
        Assert.DoesNotContain(vm.Char2Tiles, tile => tile.Label == "BAFBB");
        Assert.DoesNotContain(vm.Char2Tiles, tile => tile.Label == "BAFBC");
        Assert.DoesNotContain(vm.Char2Tiles, tile => tile.Label == "BAFBD");
    }

    [Fact]
    public void Real_catalog_uses_baga_as_final_code_for_intruding_connection()
    {
        var vm = CreateRealCatalogViewModel();

        vm.SelectGroup("BA");
        vm.SelectCode("BAG");
        vm.MeterStart = "1.00";
        vm.Q1Value = "10";
        vm.ClockVon = "12";

        Assert.Equal("BAGA", vm.FinalCode);
        Assert.Equal("Anschluss einragend", vm.FinalLabel);
        Assert.Equal("%", vm.Q1Rule?.Einheit);
        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void Existing_baga_reopens_with_exact_label_and_quantification()
    {
        var existing = new ProtocolEntry
        {
            Code = "BAGA",
            Beschreibung = "Anschluss einragend",
            MeterStart = 1.0,
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BAGA",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vsa.q1"] = "10",
                    ["vsa.uhr.von"] = "12"
                }
            }
        };
        var rules = new CodeCatalogSelectionCatalog(
            new ManifestCodeCatalogProvider(FindManifestPath()));
        var vm = new VsaCodeExplorerViewModel(
            existing,
            catalog: new VsaCodeTreeSelectionCatalog(rules));

        Assert.True(vm.ShowResultPanel);
        Assert.Equal("BAG", vm.SelectedCodeKey);
        Assert.Equal("BAGA", vm.FinalCode);
        Assert.Equal("Anschluss einragend", vm.FinalLabel);
        Assert.Equal("%", vm.Q1Rule?.Einheit);
        Assert.True(vm.CanConfirm);
    }

    [Theory]
    [InlineData("BCCYY")]
    [InlineData("BDGZ")]
    [InlineData("BAFBB")]
    [InlineData("BAFHA")]
    [InlineData("BAFHZ")]
    [InlineData("BAFJA")]
    [InlineData("BAFKB")]
    [InlineData("BAFKC")]
    [InlineData("BAFKD")]
    public void Existing_non_selectable_code_cannot_be_confirmed_or_saved(string code)
    {
        var existing = new ProtocolEntry
        {
            Code = code,
            MeterStart = 1.0
        };
        var rules = new CodeCatalogSelectionCatalog(
            new ManifestCodeCatalogProvider(FindManifestPath()));
        var vm = new VsaCodeExplorerViewModel(
            existing,
            catalog: new VsaCodeTreeSelectionCatalog(rules));

        Assert.False(vm.ShowResultPanel);
        Assert.False(vm.CanConfirm);
        Assert.Empty(vm.FinalCode);
        Assert.Throws<InvalidOperationException>(() => vm.BuildProtocolEntry());
    }

    [Fact]
    public void Real_catalog_hides_non_selectable_bdg_other_reason()
    {
        var vm = CreateRealCatalogViewModel();

        vm.SelectGroup("BD");
        vm.SelectCode("BDG");

        Assert.Contains(vm.Char1Tiles, tile => tile.Label == "BDGA");
        Assert.Contains(vm.Char1Tiles, tile => tile.Label == "BDGB");
        Assert.Contains(vm.Char1Tiles, tile => tile.Label == "BDGC");
        Assert.DoesNotContain(vm.Char1Tiles, tile => tile.Label == "BDGZ");
    }

    [Fact]
    public void Protocol_entry_preview_does_not_mutate_existing_entry_before_async_save_finishes()
    {
        var existing = new ProtocolEntry
        {
            Code = "ALT",
            Beschreibung = "Alter Text",
            MeterStart = 1.2
        };
        var vm = new VsaCodeExplorerViewModel(
            existing,
            catalog: CreateSelectableCatalog("AEDXO"))
        {
            FinalCode = "AEDXO",
            FinalLabel = "Rohrmaterialwechsel",
            FinalSublabel = "Polyethylen",
            MeterStart = "9.00"
        };

        var preview = vm.BuildProtocolEntryPreview();

        Assert.NotSame(existing, preview);
        Assert.Equal("AEDXO", preview.Code);
        Assert.Equal(9.0, preview.MeterStart);
        Assert.Equal("ALT", existing.Code);
        Assert.Equal(1.2, existing.MeterStart);

        var applied = vm.BuildProtocolEntry();
        Assert.Same(existing, applied);
        Assert.Equal("AEDXO", existing.Code);
        Assert.Equal(9.0, existing.MeterStart);
    }

    [Fact]
    public void Existing_photo_display_and_original_paths_are_loaded_and_saved_separately()
    {
        var existing = new ProtocolEntry
        {
            FotoPaths = ["overlay.png"],
            OriginalFotoPaths = ["original.png"]
        };
        var vm = new VsaCodeExplorerViewModel(
            existing,
            catalog: CreateSelectableCatalog("BAA"))
        {
            FinalCode = "BAA",
            FinalLabel = "Schaden",
            MeterStart = "1.00"
        };

        Assert.Equal(["overlay.png"], vm.FotoPaths);
        Assert.Equal(["original.png"], vm.OriginalFotoPaths);

        vm.FotoPaths[0] = "new-overlay.png";
        var result = vm.BuildProtocolEntry();

        Assert.Equal(["new-overlay.png"], result.FotoPaths);
        Assert.Equal(["original.png"], result.OriginalFotoPaths);
    }

    [Fact]
    public void Existing_legacy_photos_fill_missing_original_slots_from_display_paths()
    {
        var existing = new ProtocolEntry
        {
            FotoPaths = ["legacy1.png", "legacy2.png"],
            OriginalFotoPaths = ["kept-original.png", ""]
        };

        var vm = new VsaCodeExplorerViewModel(existing);

        Assert.Equal(
            ["kept-original.png", "legacy2.png"],
            vm.OriginalFotoPaths);
    }

    private static VsaCodeExplorerViewModel CreateRealCatalogViewModel()
    {
        var rules = new CodeCatalogSelectionCatalog(
            new ManifestCodeCatalogProvider(FindManifestPath()));
        return new VsaCodeExplorerViewModel(
            catalog: new VsaCodeTreeSelectionCatalog(rules));
    }

    private static IVsaCodeSelectionCatalog CreateSelectableCatalog(params string[] codes)
        => new CodeCatalogSelectionCatalog(
            new InMemoryCodeCatalogProvider(
                codes.Select(code => new CodeDefinition
                {
                    Code = code,
                    Title = code,
                    Source = VsaKekCatalogSources.Ili,
                    IsSelectable = true,
                    CategoryPath = ["Kanal"]
                }).ToList()));

    private static string FindManifestPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AuswertungPro.Next.UI",
                "Data",
                "vsa_kek_2020_catalog_manifest.json");
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("VSA-KEK-Katalogmanifest wurde nicht gefunden.");
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
