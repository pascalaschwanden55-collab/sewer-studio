using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer VsaCatalogTreeBuilder (IST-Verhalten aus ObservationCatalogViewModel).
/// </summary>
public sealed class VsaCatalogTreeBuilderTests
{
    // ── Hilfsmethoden ─────────────────────────────────────────────────────────

    private static SimpleCatalog MakeCatalog(params (string Code, string Title)[] codes)
        => new(codes);

    private static CodeDefinition MakeCode(string code, string title, string group = "Test", List<string>? categoryPath = null)
        => new() { Code = code, Title = title, Group = group, CategoryPath = categoryPath ?? new List<string>() };

    // ── Baumstruktur aus Prefix ───────────────────────────────────────────────

    [Fact]
    public void BuildTree_gruppiert_ba_codes_in_hauptkategorie_und_unterkategorie()
    {
        var codes = new[] { MakeCode("BABAC", "Laengsriss") };
        var root = VsaCatalogTreeBuilder.BuildTree(codes, MakeCatalog());

        Assert.True(root.Children.ContainsKey("BA"), "Hauptkategorie BA fehlt");
        Assert.True(root.Children["BA"].Children.ContainsKey("BAB"), "Unterkategorie BAB fehlt");
        Assert.Contains(root.Children["BA"].Children["BAB"].Codes, c => c.Code == "BABAC");
    }

    [Fact]
    public void BuildTree_hauptkategorie_label_aus_maincategorylabels()
    {
        var codes = new[] { MakeCode("BABAC", "Laengsriss") };
        var root = VsaCatalogTreeBuilder.BuildTree(codes, MakeCatalog());

        Assert.Equal("Struktur der Rohrleitungen", root.Children["BA"].Label);
    }

    [Fact]
    public void BuildTree_kurze_codes_landen_direkt_unter_root()
    {
        var codes = new[] { MakeCode("BC", "Bestandsaufnahme") };
        var root = VsaCatalogTreeBuilder.BuildTree(codes, MakeCatalog());

        // Weniger als 3 Zeichen -> kein Prefix -> direkt in root.Codes
        Assert.Contains(root.Codes, c => c.Code == "BC");
    }

    [Fact]
    public void BuildTree_categorypath_hat_vorrang_vor_prefix()
    {
        var codes = new[] { MakeCode("BABAC", "Laengsriss", categoryPath: new List<string> { "Benutzerdefiniert" }) };
        var root = VsaCatalogTreeBuilder.BuildTree(codes, MakeCatalog());

        Assert.True(root.Children.ContainsKey("Benutzerdefiniert"));
        // Kein BA-Knoten (categoryPath hatte Vorrang)
        Assert.False(root.Children.ContainsKey("BA"));
    }

    [Fact]
    public void BuildTree_subkategorie_label_aus_katalog()
    {
        var codes = new[] { MakeCode("BABAC", "Laengsriss") };
        var catalog = MakeCatalog(("BAB", "Riss aus Katalog"));
        var root = VsaCatalogTreeBuilder.BuildTree(codes, catalog);

        var subNode = root.Children["BA"].Children["BAB"];
        Assert.Equal("BAB  Riss aus Katalog", subNode.Label);
    }

    [Fact]
    public void BuildTree_subkategorie_label_fallback_auf_prefix_wenn_kein_katalog()
    {
        var codes = new[] { MakeCode("ZZZAA", string.Empty, group: "Unbekannt") };
        var root = VsaCatalogTreeBuilder.BuildTree(codes, MakeCatalog());

        var subNode = root.Children["ZZ"].Children["ZZZ"];
        Assert.Equal("ZZZ", subNode.Label);
    }

    // ── BuildPathToCode ───────────────────────────────────────────────────────

    [Fact]
    public void BuildPathToCode_leitet_pfad_aus_code_prefix_ab()
    {
        var code = MakeCode("BABAC", "Laengsriss");
        var path = VsaCatalogTreeBuilder.BuildPathToCode(code);

        Assert.Equal(new[] { "BA", "BAB" }, path);
    }

    [Fact]
    public void BuildPathToCode_benutzt_categorypath_wenn_gesetzt()
    {
        var code = MakeCode("BABAC", "Laengsriss", categoryPath: new List<string> { "Gruppe1", "Untergruppe" });
        var path = VsaCatalogTreeBuilder.BuildPathToCode(code);

        Assert.Equal(new[] { "Gruppe1", "Untergruppe" }, path);
    }

    [Fact]
    public void BuildPathToCode_kurzer_code_nur_hauptpfad()
    {
        var code = MakeCode("BA", "Struktur");
        var path = VsaCatalogTreeBuilder.BuildPathToCode(code);

        Assert.Equal(new[] { "BA" }, path);
    }

    // ── FormatCatalogLabel ────────────────────────────────────────────────────

    [Fact]
    public void FormatCatalogLabel_kombiniert_code_und_titel()
    {
        var def = MakeCode("BAB", "Riss");
        Assert.Equal("BAB  Riss", VsaCatalogTreeBuilder.FormatCatalogLabel("BAB", def));
    }

    [Fact]
    public void FormatCatalogLabel_leerer_titel_gibt_nur_code_zurueck()
    {
        var def = MakeCode("BAB", string.Empty);
        Assert.Equal("BAB", VsaCatalogTreeBuilder.FormatCatalogLabel("BAB", def));
    }

    // ── ExtractSubGroupName ───────────────────────────────────────────────────

    [Fact]
    public void ExtractSubGroupName_bis_doppelpunkt_kuerzen()
    {
        var def = MakeCode("BBA", "Wurzeln: Typ A");
        Assert.Equal("Wurzeln", VsaCatalogTreeBuilder.ExtractSubGroupName(def));
    }

    [Fact]
    public void ExtractSubGroupName_kein_doppelpunkt_ganzer_titel()
    {
        var def = MakeCode("BBA", "Bewuchs");
        Assert.Equal("Bewuchs", VsaCatalogTreeBuilder.ExtractSubGroupName(def));
    }

    // ── MainCategoryLabels ─────────────────────────────────────────────────────

    [Fact]
    public void MainCategoryLabels_enthaelt_ba_und_bb()
    {
        Assert.True(VsaCatalogTreeBuilder.MainCategoryLabels.ContainsKey("BA"));
        Assert.True(VsaCatalogTreeBuilder.MainCategoryLabels.ContainsKey("BB"));
    }

    // ── Hilfklassen ──────────────────────────────────────────────────────────

    private sealed class SimpleCatalog : ICodeCatalogProvider
    {
        private readonly Dictionary<string, CodeDefinition> _lookup;

        public SimpleCatalog((string Code, string Title)[] codes)
        {
            _lookup = codes.ToDictionary(
                c => c.Code,
                c => new CodeDefinition { Code = c.Code, Title = c.Title },
                StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<CodeDefinition> GetAll() => _lookup.Values.ToList();

        public bool TryGet(string code, out CodeDefinition def)
            => _lookup.TryGetValue(code, out def!);

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Read-only test catalog");

        public IReadOnlyList<string> AllowedCodes() => _lookup.Keys.ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
