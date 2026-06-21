using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionDisplayPolicyTests
{
    [Fact]
    public void CompactModelName_keeps_last_path_segment_and_handles_empty_values()
    {
        Assert.Equal("?", LiveDetectionDisplayPolicy.CompactModelName(null));
        Assert.Equal("qwen3-vl:8b", LiveDetectionDisplayPolicy.CompactModelName("models/qwen3-vl:8b"));
        Assert.Equal("local-model", LiveDetectionDisplayPolicy.CompactModelName(" local-model "));
    }

    [Fact]
    public void BuildDetectionLabel_includes_clock_extent_measurements_and_truncated_code_label()
    {
        var finding = new LiveFrameFinding(
            "sehr-langer-befundname-mit-zusatz",
            4,
            "3",
            25,
            VsaCodeHint: "BAB",
            HeightMm: 12,
            IntrusionPercent: 8);

        var label = LiveDetectionDisplayPolicy.BuildDetectionLabel(finding);

        Assert.Equal("3 / 25% H:12mm Einr:8% - BAB sehr-langer-befundna...", label);
    }

    [Fact]
    public void BuildFindingAssignmentTooltip_includes_label_code_hint_and_severity()
    {
        var finding = new LiveFrameFinding(
            "Riss",
            4,
            "3",
            20,
            VsaCodeHint: "BAB");

        var tooltip = LiveDetectionDisplayPolicy.BuildFindingAssignmentTooltip(finding);

        Assert.Equal("Klick: Schadenscode zuweisen\nRiss\nVorschlag: BAB\nSchwere: 4/5", tooltip);
    }

    [Fact]
    public void BuildDetectionConfirmationTitle_formats_single_finding()
    {
        var findings = new[]
        {
            new LiveFrameFinding("Riss", 4, "3", 20)
        };

        var title = LiveDetectionDisplayPolicy.BuildDetectionConfirmationTitle(findings);

        Assert.Equal("KI-Erkennung: Riss (S4 schwer)", title);
    }

    [Fact]
    public void BuildDetectionConfirmationTitle_formats_multiple_findings()
    {
        var findings = new[]
        {
            new LiveFrameFinding("Riss", 3, "3", 20),
            new LiveFrameFinding("Wurzel", 2, "9", 10)
        };

        var title = LiveDetectionDisplayPolicy.BuildDetectionConfirmationTitle(findings);

        Assert.Equal("KI-Erkennung: 2 Befunde - Riss (S3 mittel)", title);
    }

    [Fact]
    public void BuildDetectionConfirmationDetails_formats_clock_label_and_extent()
    {
        var findings = new[]
        {
            new LiveFrameFinding("Riss", 3, "3", 20),
            new LiveFrameFinding("Wurzel", 2, null, null)
        };

        var details = LiveDetectionDisplayPolicy.BuildDetectionConfirmationDetails(findings);

        Assert.Equal("3 Uhr - Riss - 20%  |  ? Uhr - Wurzel", details);
    }

    [Fact]
    public void QuickScanSeverityColor_uses_gray_for_clean_segments_and_severity_colors_for_damage()
    {
        Assert.Equal(Color.FromArgb(100, 0x94, 0xA3, 0xB8), LiveDetectionDisplayPolicy.QuickScanSeverityColor(5, hasDamage: false));
        Assert.Equal(Color.FromRgb(0xEF, 0x44, 0x44), LiveDetectionDisplayPolicy.QuickScanSeverityColor(4, hasDamage: true));
        Assert.Equal(Color.FromRgb(0x22, 0xC5, 0x5E), LiveDetectionDisplayPolicy.QuickScanSeverityColor(1, hasDamage: true));
    }

    [Fact]
    public void BuildQuickScanTooltip_describes_damage_segments_with_optional_clock()
    {
        var segment = new QuickScanSegment(
            TimestampSeconds: 12.25,
            HasDamage: true,
            Severity: 4,
            Label: "Riss",
            Clock: "3");

        var tooltip = LiveDetectionDisplayPolicy.BuildQuickScanTooltip(segment);

        Assert.Equal("Schaden: Riss (Schwere 4)\nUhr: 3\n@ 12.3s", tooltip);
    }

    [Fact]
    public void BuildQuickScanTooltip_describes_clean_segments()
    {
        var segment = new QuickScanSegment(
            TimestampSeconds: 8,
            HasDamage: false,
            Severity: 0,
            Label: null,
            Clock: null);

        var tooltip = LiveDetectionDisplayPolicy.BuildQuickScanTooltip(segment);

        Assert.Equal("Kein Schaden @ 8.0s", tooltip);
    }

    [Fact]
    public void BuildDetectionStatusText_formats_error_clean_and_damage_results()
    {
        var error = new LiveDetection(12, [], null, "Timeout");
        var clean = new LiveDetection(8, [], null, null);
        var damaged = new LiveDetection(4.25, [new LiveFrameFinding("Riss", 3, "3", 20)], null, null);

        Assert.Equal("Fehler: Timeout", LiveDetectionDisplayPolicy.BuildDetectionStatusText(error));
        Assert.Equal("Kein Schaden @ 8.0s", LiveDetectionDisplayPolicy.BuildDetectionStatusText(clean));
        Assert.Equal("1 Schaden erkannt @ 4.3s", LiveDetectionDisplayPolicy.BuildDetectionStatusText(damaged));
    }

    [Fact]
    public void BuildCodingNoDamageStatusText_formats_optional_osd_meter()
    {
        Assert.Equal("OSD 12.35m – Kein Schaden", LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(12.345));
        Assert.Equal("Kein Schaden", LiveDetectionDisplayPolicy.BuildCodingNoDamageStatusText(null));
    }

    [Fact]
    public void BuildCodingFindingsStatusText_formats_count_with_optional_osd_meter()
    {
        Assert.Equal("OSD 4.20m – 3 Befund(e)", LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(4.2, 3));
        Assert.Equal("3 Befund(e)", LiveDetectionDisplayPolicy.BuildCodingFindingsStatusText(null, 3));
    }

    [Fact]
    public void BuildFindingSummaryText_uses_code_hint_and_limits_to_three_findings()
    {
        var findings = new[]
        {
            new LiveFrameFinding("Riss", 3, "3", 20, VsaCodeHint: "BAB"),
            new LiveFrameFinding("Wurzel", 4, "9", 20),
            new LiveFrameFinding("Anschluss", 2, "12", 10, VsaCodeHint: "BCA"),
            new LiveFrameFinding("Inkrustation", 1, "6", 5, VsaCodeHint: "BBB")
        };

        var summary = LiveDetectionDisplayPolicy.BuildFindingSummaryText(findings);

        Assert.Equal("BAB (S3) | Wurzel (S4) | BCA (S2)", summary);
    }

    [Fact]
    public void DetectionSeverityColor_clamps_to_supported_range()
    {
        Assert.Equal(Color.FromRgb(34, 197, 94), LiveDetectionDisplayPolicy.DetectionSeverityColor(0));
        Assert.Equal(Color.FromRgb(239, 68, 68), LiveDetectionDisplayPolicy.DetectionSeverityColor(9));
    }
}
