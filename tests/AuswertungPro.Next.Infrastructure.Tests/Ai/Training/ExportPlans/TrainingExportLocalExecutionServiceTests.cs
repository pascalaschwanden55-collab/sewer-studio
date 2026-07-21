using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingExportLocalExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_gibt_unveraenderten_Plan_an_den_lokalen_Ausfuehrer()
    {
        var bundle = Bundle();
        var executor = new FakeExecutor(bundle);
        var datasetRoot = Path.Combine(Path.GetTempPath(), "local-export-service-tests", "datasets");
        var service = new TrainingExportLocalExecutionService(executor, datasetRoot);

        var result = await service.ExecuteAsync(bundle);

        Assert.Equal(TrainingExportExecutionRoute.LocalRequested, result.Route);
        Assert.Same(bundle, executor.LastBundle);
        Assert.Equal(Path.GetFullPath(datasetRoot), executor.LastDatasetRoot);
        Assert.Equal(bundle.Plan.PlanId, result.Result.PlanId);
    }

    private static TrainingExportPlanBundle Bundle()
    {
        var plan = new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            new string('a', 64),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
            "inventory-run",
            new Dictionary<string, string> { ["source"] = new string('b', 64) },
            2,
            new string('c', 64),
            new string('d', 64),
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                new string('e', 64))],
            YoloDetectClassMapV2.Classes.OrderBy(item => item.Value).Select(item => item.Key).ToArray(),
            ["100-200"],
            [],
            new Dictionary<string, int> { ["BAB_riss"] = 1 },
            [new TrainingExportPlannedImage(
                new string('f', 64),
                "100-200",
                TrainingExportTarget.Train,
                $"img_{new string('f', 64)}.png",
                [new TrainingExportPlannedLabel(
                    1,
                    "BAB_riss",
                    new TrainingExportBoundingBox(0.5, 0.5, 0.2, 0.1),
                    [new TrainingExportSourceRef(TrainingExportSourceType.TeacherAnnotation, "teacher-a")])])],
            []);
        return new TrainingExportPlanBundle(
            plan,
            new Dictionary<string, string> { [new string('f', 64)] = @"C:\frame.png" });
    }

    private sealed class FakeExecutor(TrainingExportPlanBundle expected) : ITrainingExportPlanLocalExecutor
    {
        public TrainingExportPlanBundle? LastBundle { get; private set; }
        public string? LastDatasetRoot { get; private set; }

        public Task<TrainingExportExecutionResult> ExecuteAsync(
            TrainingExportPlanBundle bundle,
            string datasetRoot,
            CancellationToken cancellationToken = default)
        {
            Assert.Same(expected, bundle);
            LastBundle = bundle;
            LastDatasetRoot = datasetRoot;
            return Task.FromResult(new TrainingExportExecutionResult(
                bundle.Plan.PlanId,
                bundle.Plan.PlanId,
                TrainingExportExecutionStatus.Created,
                1,
                1,
                0,
                bundle.Plan.Classes.Count,
                Path.Combine(datasetRoot, bundle.Plan.PlanId),
                Path.Combine(datasetRoot, bundle.Plan.PlanId, "data.yaml"),
                Path.Combine(datasetRoot, bundle.Plan.PlanId, "manifest.json"),
                bundle.Plan.Images.Select(image => image.ImageSha256).ToArray()));
        }
    }
}
