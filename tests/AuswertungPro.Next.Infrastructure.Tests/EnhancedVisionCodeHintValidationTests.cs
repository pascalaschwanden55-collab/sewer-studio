using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer die Katalog-Validierung des vsa_code_hint. Erfundene (halluzinierte)
/// Codes muessen verworfen werden, BEVOR sie in Dedup/Tracking landen — echte
/// Katalog-Codes (auch Hauptcodes) bleiben erhalten.
/// </summary>
public class EnhancedVisionCodeHintValidationTests
{
    private static readonly IReadOnlySet<string> Known =
        new HashSet<string>(new[] { "BAB", "BCA", "BCAEB" }, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ValidateCodeHint_KnownSubcode_IsKept()
        => Assert.Equal("BCAEB", EnhancedVisionAnalysisService.ValidateCodeHint("BCAEB", Known));

    [Fact]
    public void ValidateCodeHint_InventedSubcode_IsNulled()
        => Assert.Null(EnhancedVisionAnalysisService.ValidateCodeHint("BCAXY", Known));

    [Fact]
    public void ValidateCodeHint_KnownMainCode_CaseInsensitive_IsKept()
    {
        // Der Prompt empfiehlt Hauptcodes (BCA) wenn der Untertyp unklar ist — die duerfen nicht verworfen werden.
        var result = EnhancedVisionAnalysisService.ValidateCodeHint("bca", Known);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateCodeHint_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(EnhancedVisionAnalysisService.ValidateCodeHint(null, Known));
        Assert.Null(EnhancedVisionAnalysisService.ValidateCodeHint("   ", Known));
    }

    [Fact]
    public void ValidateCodeHint_NoCatalog_KeepsHintUnchanged()
        => Assert.Equal("EGAL", EnhancedVisionAnalysisService.ValidateCodeHint("EGAL", knownCodes: null));

    [Fact]
    public void BuildKnownCodeSet_EmptyCatalog_ReturnsNull()
        => Assert.Null(EnhancedVisionAnalysisService.BuildKnownCodeSet(new FakeCatalog()));

    [Fact]
    public void BuildKnownCodeSet_PopulatedCatalog_ContainsAllCodesCaseInsensitive()
    {
        var set = EnhancedVisionAnalysisService.BuildKnownCodeSet(
            new FakeCatalog(
                new CodeDefinition { Code = "BAB" },
                new CodeDefinition { Code = "BCA" }));

        Assert.NotNull(set);
        Assert.Contains("BAB", set!);
        Assert.Contains("bca", set!); // case-insensitiv
    }

    private sealed class FakeCatalog : ICodeCatalogProvider
    {
        private readonly List<CodeDefinition> _codes;
        public FakeCatalog(params CodeDefinition[] codes) => _codes = new List<CodeDefinition>(codes);
        public IReadOnlyList<CodeDefinition> GetAll() => _codes;
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = _codes.Find(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))!;
            return def is not null;
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) { }
        public IReadOnlyList<string> AllowedCodes() => _codes.ConvertAll(c => c.Code);
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => Array.Empty<string>();
    }
}
