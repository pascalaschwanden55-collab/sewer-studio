using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationPolicyTests
{
    [Fact]
    public void SelectSignificantFindings_keeps_findings_with_severity_two_or_higher()
    {
        var low = Finding("low", severity: 1);
        var medium = Finding("medium", severity: 2);
        var high = Finding("high", severity: 4);

        var selected = LiveDetectionConfirmationPolicy.SelectSignificantFindings(
            new[] { low, medium, high });

        Assert.Equal(new[] { medium, high }, selected);
    }

    [Fact]
    public void SelectSignificantFindings_returns_empty_list_when_no_finding_is_significant()
    {
        var selected = LiveDetectionConfirmationPolicy.SelectSignificantFindings(
            new[] { Finding("none", severity: 0), Finding("low", severity: 1) });

        Assert.Empty(selected);
    }

    private static LiveFrameFinding Finding(string label, int severity)
        => new(label, severity, PositionClock: null, ExtentPercent: null);
}
