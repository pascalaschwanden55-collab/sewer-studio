using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MarkerColorClassifierTests
{
    [Fact]
    public void Classify_rejected_marker_overrides_confidence()
    {
        Assert.Equal(MarkerColorKind.Rejected, MarkerColorClassifier.Classify(isRejected: true, confidence: 0.99));
        Assert.Equal(MarkerColorKind.Rejected, MarkerColorClassifier.Classify(isRejected: true, confidence: -1));
    }

    [Fact]
    public void Classify_negative_confidence_marks_manual_entry()
    {
        Assert.Equal(MarkerColorKind.Manual, MarkerColorClassifier.Classify(isRejected: false, confidence: -1));
    }

    [Theory]
    [InlineData(0.85, "Green")]
    [InlineData(1.00, "Green")]
    [InlineData(0.84, "Yellow")]
    [InlineData(0.60, "Yellow")]
    [InlineData(0.59, "Red")]
    [InlineData(double.NaN, "Red")]
    public void Classify_keeps_existing_quality_gate_thresholds(double confidence, string expected)
        => Assert.Equal(expected, MarkerColorClassifier.Classify(isRejected: false, confidence).ToString());
}
