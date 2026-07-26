using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingNewFindingOverlaySelectorTests
{
    [Fact]
    public void Select_keeps_only_findings_that_are_not_known()
    {
        var known = Finding("known");
        var fresh = Finding("fresh");

        var selected = CodingNewFindingOverlaySelector.Select(
            [known, fresh],
            currentMeter: 12.5,
            isKnown: (finding, meter) => ReferenceEquals(finding, known) && meter == 12.5);

        var item = Assert.Single(selected);
        Assert.Same(fresh, item);
    }

    [Fact]
    public void Select_preserves_input_order_for_fresh_findings()
    {
        var first = Finding("first");
        var second = Finding("second");

        var selected = CodingNewFindingOverlaySelector.Select(
            [first, second],
            currentMeter: 3,
            isKnown: (_, _) => false);

        Assert.Collection(
            selected,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
    }

    private static LiveFrameFinding Finding(string label)
        => new(
            Label: label,
            Severity: 1,
            PositionClock: null,
            ExtentPercent: null);
}
