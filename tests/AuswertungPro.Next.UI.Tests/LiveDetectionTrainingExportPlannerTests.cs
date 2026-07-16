using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTrainingExportPlannerTests
{
    [Fact]
    public void BuildAccepted_uses_hint_code_class_id_bbox_and_detection_base_name()
    {
        var finding = new LiveFrameFinding(
            "Fallback",
            3,
            "12",
            25,
            VsaCodeHint: "BAB");

        var plan = CreatePlanner().PlanAccepted(finding, "abc123");

        Assert.Equal("BAB", plan.Code);
        Assert.Equal(7, plan.ClassId);
        Assert.Equal("det_abc123", plan.BaseName);
        Assert.Equal(0.5, plan.BoundingBox.XCenter, precision: 3);
        Assert.Equal(0.15, plan.BoundingBox.YCenter, precision: 3);
        Assert.Equal(0.15, plan.BoundingBox.Width, precision: 3);
        Assert.Equal(0.15, plan.BoundingBox.Height, precision: 3);
    }

    [Fact]
    public void BuildAccepted_falls_back_to_label_when_hint_is_missing()
    {
        var finding = new LiveFrameFinding("BBA", 4, "9", 20);

        var plan = CreatePlanner().PlanAccepted(finding, "def456");

        Assert.Equal("BBA", plan.Code);
        Assert.Equal(11, plan.ClassId);
        Assert.Equal("det_def456", plan.BaseName);
    }

    [Fact]
    public void BuildCorrected_uses_selected_code_and_corrected_base_name()
    {
        var finding = new LiveFrameFinding("KI-Vorschlag", 2, "3", 20, VsaCodeHint: "BAB");

        var plan = CreatePlanner().PlanCorrected(finding, "BCA", "corr789");

        Assert.Equal("BCA", plan.Code);
        Assert.Equal(2, plan.ClassId);
        Assert.Equal("det_corr_corr789", plan.BaseName);
        Assert.Equal(0.85, plan.BoundingBox.XCenter, precision: 3);
        Assert.Equal(0.5, plan.BoundingBox.YCenter, precision: 3);
    }

    [Fact]
    public void CreateAnnotationId_returns_twelve_lowercase_hex_characters()
    {
        var annotationId = LiveDetectionTrainingExportPlanner.CreateAnnotationId();

        Assert.Matches("^[0-9a-f]{12}$", annotationId);
    }

    private static LiveDetectionTrainingExportPlanner CreatePlanner()
        => new(new FixedClassMap());

    private sealed class FixedClassMap : IVsaYoloClassMapStore
    {
        private static readonly Dictionary<string, int> Classes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BAB"] = 7,
            ["BBA"] = 11,
            ["BCA"] = 2
        };

        public int GetClassId(string vsaCode) => Classes[vsaCode];

        public Dictionary<string, int> GetFullMap() => new(Classes, StringComparer.OrdinalIgnoreCase);

        public Task ExportClassesTxtAsync(string outputPath) => Task.CompletedTask;
    }
}
