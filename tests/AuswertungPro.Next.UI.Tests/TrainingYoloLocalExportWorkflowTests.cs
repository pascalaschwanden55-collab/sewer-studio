using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloLocalExportWorkflowTests
{
    [Fact]
    public async Task RunAsync_exportiert_nur_training_samples_mit_echter_bbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewer-yolo-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var frame = Path.Combine(root, "frame.jpg");
            await File.WriteAllBytesAsync(frame, [1, 2, 3]);
            var output = Path.Combine(root, "out");
            var now = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
            var persisted = 0;
            var logs = new List<string>();
            var statuses = new List<string>();
            string? exportedClassesPath = null;

            var exported = new TrainingSample
            {
                SampleId = "sample-a",
                CaseId = "haltung-a",
                Code = "BAB",
                FramePath = frame,
                Status = TrainingSampleStatus.Approved,
                BboxXCenter = 0.1,
                BboxYCenter = 0.2,
                BboxWidth = 0.3,
                BboxHeight = 0.4
            };
            var skipped = new TrainingSample
            {
                SampleId = "sample-b",
                CaseId = "haltung-b",
                Code = "BCA",
                FramePath = frame,
                Status = TrainingSampleStatus.Approved
            };

            await TrainingYoloLocalExportWorkflow.RunAsync(
                new TrainingYoloLocalExportWorkflowRequest(
                    ApprovedSamples: [exported, skipped],
                    OutputDir: output,
                    EvalImageHashes: new HashSet<string>(),
                    EvalHaltungKeys: new HashSet<string>(),
                    LoadAnnotationsAsync: () => Task.FromResult<IReadOnlyList<TeacherAnnotation>>([]),
                    PersistSamplesAsync: () =>
                    {
                        persisted++;
                        return Task.CompletedTask;
                    },
                    Log: logs.Add,
                    SetProgressMax: _ => { },
                    SetProgressValue: _ => { },
                    SetStatusText: statuses.Add,
                    GetClassId: code => string.Equals(code, "BAB", StringComparison.OrdinalIgnoreCase) ? 7 : 2,
                    GetFullClassMap: () => new Dictionary<string, int>
                    {
                        ["BCA"] = 2,
                        ["BAB"] = 7
                    },
                    ExportClassesTxtAsync: path =>
                    {
                        exportedClassesPath = path;
                        return File.WriteAllTextAsync(path, "BCA\nBAB\n");
                    },
                    UtcNow: () => now,
                    CancellationToken: CancellationToken.None));

            var label = await File.ReadAllTextAsync(Path.Combine(output, "labels", "train", "sample_000000.txt"));
            var yaml = await File.ReadAllTextAsync(Path.Combine(output, "data.yaml"));

            Assert.True(File.Exists(Path.Combine(output, "images", "train", "sample_000000.jpg")));
            Assert.False(File.Exists(Path.Combine(output, "images", "val", "sample_000001.jpg")));
            Assert.Equal("7 0.100000 0.200000 0.300000 0.400000", label);
            Assert.Contains("names: ['BCA', 'BAB']", yaml);
            Assert.Equal(Path.Combine(output, "classes.txt"), exportedClassesPath);
            Assert.Equal(now, exported.ExportedUtc);
            Assert.Null(skipped.ExportedUtc);
            Assert.Equal(1, persisted);
            Assert.Contains(logs, line => line.Contains("Exportiere 1 TrainingSamples mit echter Box", StringComparison.Ordinal));
            Assert.Contains(statuses, text => text.StartsWith("YOLO-Export fertig:", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
