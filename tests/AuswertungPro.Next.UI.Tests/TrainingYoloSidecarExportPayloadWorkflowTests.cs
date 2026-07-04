using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingYoloSidecarExportPayloadWorkflowTests
{
    [Fact]
    public async Task BuildAsync_liest_frames_und_filtert_eval_und_samples_ohne_bbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewer-yolo-sidecar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var frame = Path.Combine(root, "frame.jpg");
            await File.WriteAllBytesAsync(frame, [4, 5, 6]);
            var progressValues = new List<int>();
            var statuses = new List<string>();

            var exported = new TrainingSample
            {
                CaseId = "haltung-clean",
                Code = "BAB",
                FramePath = frame,
                BboxXCenter = 0.1,
                BboxYCenter = 0.2,
                BboxWidth = 0.3,
                BboxHeight = 0.4
            };
            var noBox = new TrainingSample
            {
                CaseId = "haltung-no-box",
                Code = "BCA",
                FramePath = frame
            };
            var evalHaltung = new TrainingSample
            {
                CaseId = "06.24379-06.24377",
                Code = "BAC",
                FramePath = frame,
                BboxXCenter = 0.5,
                BboxYCenter = 0.5,
                BboxWidth = 0.2,
                BboxHeight = 0.2
            };

            var result = await TrainingYoloSidecarExportPayloadWorkflow.BuildAsync(
                new TrainingYoloSidecarExportPayloadRequest(
                    ApprovedSamples: [exported, noBox, evalHaltung],
                    OutputDir: "out-dir",
                    TrainSplit: 0.8,
                    EvalImageHashes: new HashSet<string>(),
                    EvalHaltungKeys: new HashSet<string> { "24379-24377" },
                    SetProgressMax: _ => { },
                    SetProgressValue: progressValues.Add,
                    SetStatusText: statuses.Add,
                    CancellationToken: CancellationToken.None));

            var sample = Assert.Single(result.ExportRequest.Samples);
            var label = Assert.Single(sample.Labels);

            Assert.Equal("out-dir", result.ExportRequest.OutputDir);
            Assert.Equal(0.8, result.ExportRequest.TrainSplit);
            Assert.Equal(Convert.ToBase64String([4, 5, 6]), sample.ImageBase64);
            Assert.Equal("BAB", label.ClassName);
            Assert.Equal(0.1, label.XCenter);
            Assert.Equal(0.2, label.YCenter);
            Assert.Equal(0.3, label.Width);
            Assert.Equal(0.4, label.Height);
            Assert.Equal(0, result.SkipEvalHash);
            Assert.Equal(1, result.SkipEvalCase);
            Assert.Equal(1, result.SkipNoBox);
            Assert.Equal([0, 1, 2, 3], progressValues);
            Assert.Contains(statuses, text => text == "YOLO-Export: Lade Frame 1/3...");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
