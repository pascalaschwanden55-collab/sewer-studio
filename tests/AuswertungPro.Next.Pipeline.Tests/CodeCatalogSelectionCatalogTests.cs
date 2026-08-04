using AuswertungPro.Next.Application.Protocol;

public sealed class CodeCatalogSelectionCatalogTests
{
    [Fact]
    public void Builds_selectable_tree_from_code_catalog()
    {
        var catalog = CreateCatalog();

        Assert.True(catalog.Groups.ContainsKey("BA"));
        Assert.True(catalog.Groups["BA"].Codes.ContainsKey("BAGA"));
        Assert.False(catalog.Groups.TryGetValue("BC", out var bc) && bc.Codes.ContainsKey("BCCYY"));

        var baga = catalog.Groups["BA"].Codes["BAGA"];
        Assert.Equal("BAG", baga.CanonicalCode);
        Assert.Equal(VsaKekCatalogSources.Ili, baga.Source);
    }

    [Fact]
    public void Keeps_standard_annotation_but_hides_fallback_codes()
    {
        var catalog = CreateCatalog();

        var bdb = catalog.Groups["BD"].Codes["BDBA"];
        Assert.Equal("BDB", bdb.CanonicalCode);
        Assert.Equal("A", bdb.StandardAnnotation);

        Assert.False(catalog.Groups.ContainsKey("BZ"));
    }

    [Fact]
    public void Reads_quant_and_clock_rules_from_code_parameters()
    {
        var catalog = CreateCatalog();

        var (q1, q2) = catalog.GetQuantRule("BAGA", null);
        Assert.NotNull(q1);
        Assert.Equal("P", q1!.Pflicht);
        Assert.Equal("%", q1.Einheit);
        Assert.Null(q2);

        var clock = catalog.GetClockRule("BAGA");
        Assert.Equal("range", clock.Mode);
    }

    [Fact]
    public void Looks_up_exact_catalog_clear_text_without_parent_fallback()
    {
        var catalog = CreateCatalog();

        Assert.Equal("Einragender Anschluss", catalog.LookupExactLabel("baga"));
        Assert.Null(catalog.LookupExactLabel("BAG"));
        Assert.Null(catalog.LookupExactLabel("BZZ"));
        Assert.True(catalog.IsSelectableCode("BAGA"));
        Assert.False(catalog.IsSelectableCode("BZZ"));
        Assert.False(catalog.IsSelectableCode("BCCYY"));
    }

    [Fact]
    public void WinCan_navigation_prompts_never_replace_code_clear_text()
    {
        var catalog = new CodeCatalogSelectionCatalog(
            new InMemoryCodeCatalogProvider(
            [
                new CodeDefinition
                {
                    Code = "BCC",
                    Title = "Bogen",
                    Source = VsaKekCatalogSources.Heading
                },
                new CodeDefinition
                {
                    Code = "BCCA",
                    Title = "Vertikale Richtung",
                    Source = VsaKekCatalogSources.WinCanFallback
                },
                new CodeDefinition
                {
                    Code = "BCCAA",
                    Title = "Bogen nach links oben",
                    Source = VsaKekCatalogSources.Ili
                },
                new CodeDefinition
                {
                    Code = "BCCAB",
                    Title = "Bogen nach links unten",
                    Source = VsaKekCatalogSources.Ili
                },
                new CodeDefinition
                {
                    Code = "BCCAY",
                    Title = "Bogen nach links",
                    Source = VsaKekCatalogSources.Ili
                }
            ]));

        Assert.Null(catalog.LookupExactLabel("BCCA"));
        Assert.Equal("Bogen nach links", catalog.LookupNavigationLabel("BCCA"));
        Assert.Equal("Bogen nach links oben", catalog.LookupExactLabel("BCCAA"));
    }

    [Fact]
    public void Definition_without_authoritative_source_is_not_selectable()
    {
        var catalog = new CodeCatalogSelectionCatalog(
            new InMemoryCodeCatalogProvider(
            [
                new CodeDefinition
                {
                    Code = "BCCA",
                    Title = "Vertikale Richtung",
                    IsSelectable = true
                }
            ]));

        Assert.False(catalog.IsSelectableCode("BCCA"));
        Assert.Null(catalog.LookupExactLabel("BCCA"));
        Assert.Empty(catalog.Groups);
    }

    [Fact]
    public void Empty_selection_catalog_fails_closed()
    {
        Assert.False(EmptyVsaCodeSelectionCatalog.Instance.IsSelectableCode("BCAAA"));
    }

    [Fact]
    public void Curated_quant_rules_supply_visible_units_and_ranges()
    {
        var rules = new CodeCatalogSelectionCatalog(
            new ManifestCodeCatalogProvider(FindManifestPath()));
        var catalog = new VsaCodeTreeSelectionCatalog(rules);

        var (bcaQ1, bcaQ2) = catalog.GetQuantRule("BCA", "A");
        Assert.Equal("mm", bcaQ1?.Einheit);
        Assert.Equal("Anschlussh\u00f6he", bcaQ1?.Label);
        Assert.Equal(0, bcaQ1?.Min);
        Assert.Equal(10000, bcaQ1?.Max);
        Assert.Equal("mm", bcaQ2?.Einheit);
        Assert.Equal(0, bcaQ2?.Min);
        Assert.Equal(10000, bcaQ2?.Max);

        var (bccQ1, bccQ2) = catalog.GetQuantRule("BCC", "A");
        Assert.Equal("\u00b0", bccQ1?.Einheit);
        Assert.Equal("Richtungs\u00e4nderung", bccQ1?.Label);
        Assert.Equal(1, bccQ1?.Min);
        Assert.Equal(359, bccQ1?.Max);
        Assert.Null(bccQ2);
        Assert.Equal("none", catalog.GetClockRule("BCC").Mode);
        Assert.Equal("single", catalog.GetClockRule("BCA").Mode);
    }

    [Fact]
    public void Real_manifest_and_curated_tree_render_pe_as_polyethylene()
    {
        var rules = new CodeCatalogSelectionCatalog(
            new ManifestCodeCatalogProvider(FindManifestPath()));
        var catalog = new VsaCodeTreeSelectionCatalog(rules);
        var codeDef = catalog.Groups["AE"].Codes["AED"];
        var charDef = codeDef.Char1!["O"];
        var (q1, _) = catalog.GetQuantRule("AED", "O");

        var tile = VsaTileDataFactory.ForChar1(
            "O",
            charDef,
            "AED",
            xPrefix: true,
            hasC2: false,
            q1: q1,
            groupColor: null,
            catalogLabel: catalog.LookupExactLabel("AEDXO"),
            parentCatalogLabel: catalog.LookupExactLabel("AED"));

        Assert.Equal("AEDXO", tile.Label);
        Assert.Equal("PE · Polyethylen", tile.Description);
    }

    private static CodeCatalogSelectionCatalog CreateCatalog()
        => new(new InMemoryCodeCatalogProvider(new[]
        {
            new CodeDefinition
            {
                Code = "BAGA",
                Title = "Einragender Anschluss",
                Source = VsaKekCatalogSources.Ili,
                CanonicalCode = "BAG",
                IsSelectable = true,
                CategoryPath = ["Kanal"],
                Parameters =
                [
                    new CodeParameter { DataKey = "Q1", Name = "Einragung", Unit = "%", Required = true },
                    new CodeParameter { DataKey = "SchadenlageAnfang", Name = "Lage von" },
                    new CodeParameter { DataKey = "SchadenlageEnde", Name = "Lage bis" }
                ]
            },
            new CodeDefinition
            {
                Code = "BDBA",
                Title = "Allgemeine Anmerkung A",
                Source = VsaKekCatalogSources.Ili,
                CanonicalCode = "BDB",
                StandardAnnotation = "A",
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            },
            new CodeDefinition
            {
                Code = "BCCYY",
                Title = "Beobachtete Erweiterung",
                Source = VsaKekCatalogSources.XtfObserved,
                IsObservedExtension = true,
                IsSelectable = false,
                CategoryPath = ["Kanal"]
            },
            new CodeDefinition
            {
                Code = "BZZ",
                Title = "Alter Vergleichscode",
                Source = VsaKekCatalogSources.WinCanFallback,
                IsSelectable = true,
                CategoryPath = ["Kanal"]
            }
        }));

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
