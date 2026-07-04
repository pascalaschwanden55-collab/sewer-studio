using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloSidecarExportCompletionWorkflowTests
{
    [Fact]
    public async Task RunAsync_markiert_samples_persistiert_und_meldet_sidecar_export()
    {
        var now = new DateTime(2026, 7, 4, 13, 30, 0, DateTimeKind.Utc);
        var utcCalls = 0;
        var persisted = 0;
        var logs = new List<string>();
        var statuses = new List<string>();
        var first = new TrainingSample { SampleId = "sample-a" };
        var second = new TrainingSample { SampleId = "sample-b" };
        var response = new TrainingExportResponseDto(
            TotalSamples: 2,
            TrainCount: 1,
            ValCount: 1,
            ClassesUsed: ["BAB", "BCA"],
            DataYamlPath: @"D:\Yolo\data.yaml");

        await TrainingYoloSidecarExportCompletionWorkflow.RunAsync(
            new TrainingYoloSidecarExportCompletionRequest(
                ApprovedSamples: [first, second],
                Response: response,
                OutputDir: @"D:\Yolo",
                PersistSamplesAsync: () =>
                {
                    persisted++;
                    Assert.Equal(now, first.ExportedUtc);
                    Assert.Equal(now, second.ExportedUtc);
                    return Task.CompletedTask;
                },
                Log: logs.Add,
                SetStatusText: statuses.Add,
                UtcNow: () =>
                {
                    utcCalls++;
                    return now;
                }));

        var message = "YOLO-Export fertig: 2 Samples (1 Train, 1 Val), 2 Klassen \u2192 D:\\Yolo";

        Assert.Equal(1, utcCalls);
        Assert.Equal(1, persisted);
        Assert.Equal(now, first.ExportedUtc);
        Assert.Equal(now, second.ExportedUtc);
        Assert.Equal([message], statuses);
        Assert.Equal(
            [
                message,
                @"  data.yaml: D:\Yolo\data.yaml",
                "  Klassen: BAB, BCA"
            ],
            logs);
    }
}
