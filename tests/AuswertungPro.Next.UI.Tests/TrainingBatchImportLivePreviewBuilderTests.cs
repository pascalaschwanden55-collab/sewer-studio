using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportLivePreviewBuilderTests
{
    [Fact]
    public void BuildProcessing_uses_case_id_processing_text_and_no_meter_marker()
    {
        var preview = TrainingBatchImportLivePreviewBuilder.BuildProcessing(
            caseId: "101.1-102.1",
            previewFrame: @"C:\frames\preview.jpg");

        Assert.Equal("101.1-102.1", preview.CaseInfo);
        Assert.Equal("Verarbeite...", preview.CodeInfo);
        Assert.Equal("\u2014", preview.MeterInfo);
        Assert.Equal(@"C:\frames\preview.jpg", preview.FramePath);
    }

    [Fact]
    public void BuildSample_prefers_sample_frame_and_formats_meter_range()
    {
        var sample = new TrainingSample
        {
            Code = "BAA",
            MeterStart = 12.3,
            MeterEnd = 13.7,
            FramePath = @"C:\frames\sample.jpg"
        };

        var preview = TrainingBatchImportLivePreviewBuilder.BuildSample(
            caseId: "101.1-102.1",
            sample,
            previewFrame: @"C:\frames\preview.jpg");

        Assert.Equal("101.1-102.1", preview.CaseInfo);
        Assert.Equal("BAA", preview.CodeInfo);
        Assert.Equal("12.30 \u2013 13.70 m", preview.MeterInfo);
        Assert.Equal(@"C:\frames\sample.jpg", preview.FramePath);
    }

    [Fact]
    public void BuildSample_falls_back_to_case_preview_frame()
    {
        var sample = new TrainingSample
        {
            Code = "BAA",
            MeterStart = 1,
            MeterEnd = 2,
            FramePath = ""
        };

        var preview = TrainingBatchImportLivePreviewBuilder.BuildSample(
            caseId: "101.1-102.1",
            sample,
            previewFrame: @"C:\frames\preview.jpg");

        Assert.Equal(@"C:\frames\preview.jpg", preview.FramePath);
    }
}
