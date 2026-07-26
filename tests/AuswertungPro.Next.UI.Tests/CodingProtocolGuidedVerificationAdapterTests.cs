using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolGuidedVerificationAdapterTests
{
    [Fact]
    public void Create_returns_null_when_protocol_verifier_is_missing()
    {
        var verify = CodingProtocolGuidedVerificationAdapter.Create(null);

        Assert.Null(verify);
    }

    [Fact]
    public void ToGroundTruthEntry_maps_import_event_for_guided_verification()
    {
        var importEvent = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BAG",
                Beschreibung = "Versatz bei 6 Uhr",
                MeterStart = 12.34,
                MeterEnd = 13.0,
                IsStreckenschaden = true,
                Zeit = TimeSpan.FromSeconds(9),
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Severity = "3",
                    Parameters =
                    {
                        ["vsa.uhr.von"] = "6",
                        ["vsa.uhr.bis"] = "8",
                        ["catalog.standardAnnotation"] = "A"
                    }
                }
            },
            MeterAtCapture = 12.5,
            VideoTimestamp = TimeSpan.FromSeconds(8)
        };

        var groundTruth = CodingProtocolGuidedVerificationAdapter.ToGroundTruthEntry(
            importEvent,
            @"C:\teacher\images\mark_abc123.png");

        Assert.Equal(12.34, groundTruth.MeterStart);
        Assert.Equal(13.0, groundTruth.MeterEnd);
        Assert.Equal("BAG", groundTruth.VsaCode);
        Assert.Equal("Versatz bei 6 Uhr", groundTruth.Text);
        Assert.Equal("A", groundTruth.Characterization);
        Assert.Equal("6", groundTruth.ClockPosition);
        Assert.Equal("8", groundTruth.ConnectionClock);
        Assert.Equal("3", groundTruth.Severity);
        Assert.True(groundTruth.IsStreckenschaden);
        Assert.Equal(TimeSpan.FromSeconds(9), groundTruth.Zeit);
        Assert.Equal(@"C:\teacher\images\mark_abc123.png", groundTruth.ExtractedFramePath);
        Assert.Equal(9, groundTruth.ExtractedFrameTimeSeconds);
    }

    [Fact]
    public void ToGroundTruthEntry_uses_capture_meter_when_protocol_meter_is_missing()
    {
        var importEvent = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BAA",
                Beschreibung = "Rohrbruch"
            },
            MeterAtCapture = 4.56,
            VideoTimestamp = TimeSpan.FromSeconds(11)
        };

        var groundTruth = CodingProtocolGuidedVerificationAdapter.ToGroundTruthEntry(
            importEvent,
            @"C:\teacher\images\mark_def456.png");

        Assert.Equal(4.56, groundTruth.MeterStart);
        Assert.Equal(4.56, groundTruth.MeterEnd);
        Assert.Equal(TimeSpan.FromSeconds(11), groundTruth.Zeit);
        Assert.Equal(11, groundTruth.ExtractedFrameTimeSeconds);
    }

    [Fact]
    public void ToVerificationResult_marks_guided_fallback_as_not_checked()
    {
        var result = CodingProtocolGuidedVerificationAdapter.ToVerificationResult(
            new GuidedVerificationResult(
                MeterReading: null,
                ProtocolDamageVisible: false,
                ConfirmationLevel: "nicht_sichtbar",
                ActualVsaCode: null,
                ActualLabel: null,
                ActualSeverity: 0,
                ActualClock: null,
                ExtentPercent: null,
                Explanation: "Timeout"));

        Assert.Equal("nicht_geprueft", result.ConfirmationLevel);
        Assert.False(result.DamageVisible);
        Assert.Null(result.ActualCode);
        Assert.Null(result.MeterReading);
        Assert.Equal("Timeout", result.Explanation);
    }
}
