using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingCodeMetaWriterTests
{
    [Fact]
    public void ApplyToEntry_leaves_CodeMeta_null_when_finding_has_no_metadata()
    {
        var entry = new ProtocolEntry();

        CodingLiveFindingCodeMetaWriter.ApplyToEntry(entry, "BAB", Finding());

        Assert.Null(entry.CodeMeta);
    }

    [Fact]
    public void ApplyToEntry_writes_position_clock()
    {
        var entry = new ProtocolEntry();

        CodingLiveFindingCodeMetaWriter.ApplyToEntry(entry, "BCAEB", Finding(clock: "3:00"));

        Assert.Equal("BCAEB", entry.CodeMeta!.Code);
        Assert.Equal("3:00", entry.CodeMeta.Parameters["vsa.uhr.von"]);
    }

    [Fact]
    public void ApplyToEntry_prefers_cross_section_reduction_over_intrusion()
    {
        var entry = new ProtocolEntry();

        CodingLiveFindingCodeMetaWriter.ApplyToEntry(
            entry,
            "BAB",
            Finding(intrusionPercent: 11, crossSectionReductionPercent: 22));

        Assert.Equal("22", entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"]);
    }

    [Fact]
    public void ApplyToEntry_writes_intrusion_when_cross_section_is_missing()
    {
        var entry = new ProtocolEntry();

        CodingLiveFindingCodeMetaWriter.ApplyToEntry(
            entry,
            "BAC",
            Finding(intrusionPercent: 8));

        Assert.Equal("8", entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"]);
    }

    [Fact]
    public void ApplyToEntry_ignores_zero_or_negative_percent_values()
    {
        var entry = new ProtocolEntry();

        CodingLiveFindingCodeMetaWriter.ApplyToEntry(
            entry,
            "BAC",
            Finding(intrusionPercent: 0, crossSectionReductionPercent: -1));

        Assert.Null(entry.CodeMeta);
    }

    private static LiveFrameFinding Finding(
        string? clock = null,
        int? intrusionPercent = null,
        int? crossSectionReductionPercent = null)
        => new(
            Label: "finding",
            Severity: 2,
            PositionClock: clock,
            ExtentPercent: null,
            VsaCodeHint: null,
            IntrusionPercent: intrusionPercent,
            CrossSectionReductionPercent: crossSectionReductionPercent);
}
