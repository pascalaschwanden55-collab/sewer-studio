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
    public void Keeps_standard_annotation_and_fallback_warning()
    {
        var catalog = CreateCatalog();

        var bdb = catalog.Groups["BD"].Codes["BDBA"];
        Assert.Equal("BDB", bdb.CanonicalCode);
        Assert.Equal("A", bdb.StandardAnnotation);

        var fallback = catalog.Groups["BZ"].Codes["BZZ"];
        Assert.NotNull(fallback.Warn);
        Assert.Contains("WinCan-Fallback", fallback.Warn!);
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
