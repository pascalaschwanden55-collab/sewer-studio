using System.Collections.Generic;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer EnhancedVisionPromptBuilder.
/// Prueft das IST-Verhalten der extrahierten Prompt-Bau-Methoden.
/// </summary>
public class EnhancedVisionPromptBuilderTests
{
    // ── NormalizeBbox ─────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeBbox_NormalBox_Unchanged()
    {
        var (x1, y1, x2, y2) = EnhancedVisionPromptBuilder.NormalizeBbox(
            new List<double> { 0.2, 0.3, 0.6, 0.7 });

        Assert.Equal(0.2, x1);
        Assert.Equal(0.3, y1);
        Assert.Equal(0.6, x2);
        Assert.Equal(0.7, y2);
    }

    [Fact]
    public void NormalizeBbox_InvertedCorners_AreReordered()
    {
        var (x1, y1, x2, y2) = EnhancedVisionPromptBuilder.NormalizeBbox(
            new List<double> { 0.6, 0.7, 0.2, 0.3 });

        Assert.Equal(0.2, x1);
        Assert.Equal(0.3, y1);
        Assert.Equal(0.6, x2);
        Assert.Equal(0.7, y2);
    }

    [Fact]
    public void NormalizeBbox_OutOfRange_IsClamped()
    {
        var (x1, y1, x2, y2) = EnhancedVisionPromptBuilder.NormalizeBbox(
            new List<double> { -0.1, 0.0, 1.2, 0.5 });

        Assert.Equal(0.0, x1);
        Assert.Equal(0.0, y1);
        Assert.Equal(1.0, x2);
        Assert.Equal(0.5, y2);
    }

    [Fact]
    public void NormalizeBbox_DegenerateZeroArea_ReturnsNull()
    {
        var r = EnhancedVisionPromptBuilder.NormalizeBbox(
            new List<double> { 0.5, 0.2, 0.5, 0.7 });

        Assert.Null(r.X1);
        Assert.Null(r.Y1);
        Assert.Null(r.X2);
        Assert.Null(r.Y2);
    }

    [Fact]
    public void NormalizeBbox_NullOrTooShort_ReturnsNull()
    {
        Assert.Null(EnhancedVisionPromptBuilder.NormalizeBbox(null).X1);
        Assert.Null(EnhancedVisionPromptBuilder.NormalizeBbox(new List<double> { 0.1, 0.2 }).X1);
    }

    // ── ValidateCodeHint ──────────────────────────────────────────────────────

    private static readonly IReadOnlySet<string> KnownCodes =
        new HashSet<string>(new[] { "BAB", "BCA", "BCAEB" }, System.StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ValidateCodeHint_KnownSubcode_IsKept()
        => Assert.Equal("BCAEB", EnhancedVisionPromptBuilder.ValidateCodeHint("BCAEB", KnownCodes));

    [Fact]
    public void ValidateCodeHint_InventedSubcode_IsNulled()
        => Assert.Null(EnhancedVisionPromptBuilder.ValidateCodeHint("BCAXY", KnownCodes));

    [Fact]
    public void ValidateCodeHint_KnownMainCode_CaseInsensitive_IsKept()
    {
        var result = EnhancedVisionPromptBuilder.ValidateCodeHint("bca", KnownCodes);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateCodeHint_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(EnhancedVisionPromptBuilder.ValidateCodeHint(null, KnownCodes));
        Assert.Null(EnhancedVisionPromptBuilder.ValidateCodeHint("   ", KnownCodes));
    }

    [Fact]
    public void ValidateCodeHint_NoCatalog_KeepsHintUnchanged()
        => Assert.Equal("EGAL", EnhancedVisionPromptBuilder.ValidateCodeHint("EGAL", knownCodes: null));

    // ── BuildKnownCodeSet ─────────────────────────────────────────────────────

    [Fact]
    public void BuildKnownCodeSet_EmptyCatalog_ReturnsNull()
        => Assert.Null(EnhancedVisionPromptBuilder.BuildKnownCodeSet(new FakeCatalog()));

    [Fact]
    public void BuildKnownCodeSet_PopulatedCatalog_ContainsAllCodesCaseInsensitive()
    {
        var set = EnhancedVisionPromptBuilder.BuildKnownCodeSet(
            new FakeCatalog(
                new CodeDefinition { Code = "BAB" },
                new CodeDefinition { Code = "BCA" }));

        Assert.NotNull(set);
        Assert.Contains("BAB", set!);
        Assert.Contains("bca", set!);
    }

    // ── BuildDamageClassesPrompt ──────────────────────────────────────────────

    [Fact]
    public void BuildDamageClassesPrompt_WithoutCatalog_ContainsFallbackTitles()
    {
        var prompt = EnhancedVisionPromptBuilder.BuildDamageClassesPrompt(null);

        Assert.Contains("BAA = Verformung", prompt);
        Assert.Contains("BAB = Riss", prompt);
        Assert.Contains("BBA = Wurzeln", prompt);
        Assert.Contains("BBC = Ablagerungen", prompt);
    }

    [Fact]
    public void BuildDamageClassesPrompt_WithCatalog_UsesCatalogTitle()
    {
        var catalog = new FakeCatalog(
            new CodeDefinition { Code = "BBA", Title = "Bewuchs aus Katalog" });

        var prompt = EnhancedVisionPromptBuilder.BuildDamageClassesPrompt(catalog);

        Assert.Contains("BBA = Bewuchs aus Katalog", prompt);
    }

    // ── BuildImportContextSection ─────────────────────────────────────────────

    [Fact]
    public void BuildImportContextSection_Null_ReturnsEmpty()
        => Assert.Equal("", EnhancedVisionPromptBuilder.BuildImportContextSection(null));

    [Fact]
    public void BuildImportContextSection_WithEntries_ContainsCodeAndDesc()
    {
        var section = EnhancedVisionPromptBuilder.BuildImportContextSection(
            new List<(string Code, string Description, double Meter)>
            {
                ("BDDC", "Wasserstand sichtbar", 12.3)
            });

        Assert.Contains("BDDC", section);
        Assert.Contains("Wasserstand", section);
        Assert.Contains("BEKANNTE BEFUNDE", section);
    }

    [Fact]
    public void BuildImportContextSection_DuplicateCodes_OnlyShownOnce()
    {
        var section = EnhancedVisionPromptBuilder.BuildImportContextSection(
            new List<(string Code, string Description, double Meter)>
            {
                ("BAB", "Riss laengs", 5.0),
                ("BAB", "Riss quer",  8.0)
            });

        // Doppelter Code soll nur einmal erscheinen (Deduplizierung)
        var count = 0;
        var idx = 0;
        while ((idx = section.IndexOf("- BAB:", idx, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx++;
        }
        Assert.Equal(1, count);
    }

    // ── BuildObservationHintsSection ──────────────────────────────────────────

    [Fact]
    public void BuildObservationHintsSection_Null_ReturnsEmpty()
        => Assert.Equal("", EnhancedVisionPromptBuilder.BuildObservationHintsSection(null));

    [Fact]
    public void BuildObservationHintsSection_WithHint_ContainsHintText()
    {
        var section = EnhancedVisionPromptBuilder.BuildObservationHintsSection(
            new List<string> { "riss_bruch (72 %)" });

        Assert.Contains("riss_bruch", section);
        Assert.Contains("ZUSAETZLICHE BILD-HINWEISE", section);
        Assert.Contains("nicht als VSA-Code", section);
    }

    // ── BuildPrompt ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_WithoutContext_ContainsBaseInstructions()
    {
        var prompt = EnhancedVisionPromptBuilder.BuildPrompt(null);

        Assert.Contains("METERSTAND", prompt);
        Assert.Contains("OSD", prompt);
        Assert.Contains("VSA-KEK-KATALOGAUSZUG", prompt);
        Assert.Contains("SCHWEREGRAD-SKALA", prompt);
    }

    [Fact]
    public void BuildPrompt_WithImportContext_InjectsContextSection()
    {
        var prompt = EnhancedVisionPromptBuilder.BuildPrompt(
            null,
            importContext: new List<(string, string, double)> { ("BCA", "Anschluss", 7.5) });

        Assert.Contains("BEKANNTE BEFUNDE", prompt);
        Assert.Contains("BCA", prompt);
    }

    [Fact]
    public void BuildPrompt_WithObservationHints_InjectsHintsSection()
    {
        var prompt = EnhancedVisionPromptBuilder.BuildPrompt(
            null,
            observationHints: new List<string> { "moeglicher_riss" });

        Assert.Contains("ZUSAETZLICHE BILD-HINWEISE", prompt);
        Assert.Contains("moeglicher_riss", prompt);
    }

    // ── LookupCatalogTitle ────────────────────────────────────────────────────

    [Fact]
    public void LookupCatalogTitle_NullCatalog_ReturnsNull()
        => Assert.Null(EnhancedVisionPromptBuilder.LookupCatalogTitle(null, "BCA"));

    [Fact]
    public void LookupCatalogTitle_ExactMatch_ReturnsTitle()
    {
        var catalog = new FakeCatalog(new CodeDefinition { Code = "BCA", Title = "Seitlicher Anschluss" });
        Assert.Equal("Seitlicher Anschluss", EnhancedVisionPromptBuilder.LookupCatalogTitle(catalog, "BCA"));
    }

    [Fact]
    public void LookupCatalogTitle_FallbackToMainCode_WhenSubcodeNotFound()
    {
        // Subcode BCAEB nicht im Katalog, aber Hauptcode BCA vorhanden -> Hauptcode-Fallback
        var catalog = new FakeCatalog(new CodeDefinition { Code = "BCA", Title = "Anschluss" });
        var title = EnhancedVisionPromptBuilder.LookupCatalogTitle(catalog, "BCAEB");
        Assert.Equal("Anschluss", title);
    }

    // ── Hilfsobjekte ──────────────────────────────────────────────────────────

    private sealed class FakeCatalog : ICodeCatalogProvider
    {
        private readonly List<CodeDefinition> _codes;
        public FakeCatalog(params CodeDefinition[] codes) => _codes = new List<CodeDefinition>(codes);
        public IReadOnlyList<CodeDefinition> GetAll() => _codes;
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.Find(c => string.Equals(c.Code, code, System.StringComparison.OrdinalIgnoreCase))!;
            return def is not null;
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) { }
        public IReadOnlyList<string> AllowedCodes() => _codes.ConvertAll(c => c.Code);
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => System.Array.Empty<string>();
    }
}
