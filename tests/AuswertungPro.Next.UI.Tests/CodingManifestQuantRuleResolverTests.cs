using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManifestQuantRuleResolverTests
{
    [Fact]
    public void Resolve_allows_everything_when_catalog_is_missing()
    {
        var rule = CodingManifestQuantRuleResolver.Resolve(null, "BAB");

        Assert.True(rule.HasQ1);
        Assert.True(rule.HasQ2);
        Assert.True(rule.AllowClock);
    }

    [Fact]
    public void Resolve_maps_quant_fields_and_clock_mode_from_catalog()
    {
        var catalog = new StubSelectionCatalog
        {
            Q1 = new QuantField { Label = "Q1" },
            Q2 = null,
            Clock = new ClockRule { Mode = "none" }
        };

        var rule = CodingManifestQuantRuleResolver.Resolve(catalog, "BAB");

        Assert.True(rule.HasQ1);
        Assert.False(rule.HasQ2);
        Assert.False(rule.AllowClock);
    }

    private sealed class StubSelectionCatalog : IVsaCodeSelectionCatalog
    {
        public QuantField? Q1 { get; init; }
        public QuantField? Q2 { get; init; }
        public ClockRule Clock { get; init; } = new() { Mode = "range" };

        public IReadOnlyDictionary<string, GroupDef> Groups { get; } =
            new Dictionary<string, GroupDef>(StringComparer.OrdinalIgnoreCase);

        public (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key)
            => (Q1, Q2);

        public ClockRule GetClockRule(string codeKey)
            => Clock;

        public IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key)
            => null;

        public bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key)
            => false;
    }
}
