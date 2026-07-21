using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingExportCompletionServiceTests
{
    [Fact]
    public void Apply_markiert_nur_bestaetigte_geplante_TrainingSamples()
    {
        var plan = Plan();
        var planned = new TrainingSample { SampleId = "sample-planned" };
        var excluded = new TrainingSample { SampleId = "sample-excluded" };
        var unrelated = new TrainingSample { SampleId = "sample-unrelated" };
        var exportedUtc = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

        var result = new TrainingExportCompletionService().Apply(
            plan,
            Execution(plan),
            [planned, excluded, unrelated],
            exportedUtc);

        Assert.Equal(1, result.MarkedTrainingSamples);
        Assert.Equal(["sample-planned"], result.MarkedSampleIds);
        Assert.Equal(exportedUtc, planned.ExportedUtc);
        Assert.Null(excluded.ExportedUtc);
        Assert.Null(unrelated.ExportedUtc);
    }

    [Fact]
    public void Apply_blockiert_falschen_PlanHash_ohne_Sample_zu_veraendern()
    {
        var plan = Plan();
        var sample = new TrainingSample { SampleId = "sample-planned" };
        var execution = Execution(plan) with { PlanSha256 = new string('f', 64) };

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportCompletionService().Apply(
                plan,
                execution,
                [sample],
                DateTime.UtcNow));

        Assert.Null(sample.ExportedUtc);
    }

    [Fact]
    public void Apply_blockiert_unvollstaendige_Bildbestaetigung_ohne_Teilaenderung()
    {
        var plan = Plan();
        var sample = new TrainingSample { SampleId = "sample-planned" };
        var execution = Execution(plan) with { WrittenImageSha256 = Array.Empty<string>() };

        Assert.Throws<TrainingExportPlanException>(() =>
            new TrainingExportCompletionService().Apply(
                plan,
                execution,
                [sample],
                DateTime.UtcNow));

        Assert.Null(sample.ExportedUtc);
    }

    private static TrainingExportPlan Plan()
    {
        var box = new TrainingExportBoundingBox(0.5, 0.5, 0.2, 0.1);
        var hash = new string('a', 64);
        return new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            new string('b', 64),
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"),
            "inventory-run",
            new Dictionary<string, string>
            {
                ["teacher_annotations.json"] = new string('c', 64),
                ["training_samples.json"] = new string('d', 64)
            },
            2,
            new string('e', 64),
            new string('f', 64),
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                new string('1', 64))],
            ["BCA_anschluss", "BAB_riss"],
            ["100-200"],
            [],
            new Dictionary<string, int> { ["BAB_riss"] = 1 },
            [new TrainingExportPlannedImage(
                hash,
                "100-200",
                TrainingExportTarget.Train,
                $"img_{hash}.png",
                [new TrainingExportPlannedLabel(
                    1,
                    "BAB_riss",
                    box,
                    [
                        new TrainingExportSourceRef(TrainingExportSourceType.TeacherAnnotation, "teacher-1"),
                        new TrainingExportSourceRef(TrainingExportSourceType.TrainingSample, "sample-planned")
                    ])])],
            [new TrainingExportExclusion(
                new TrainingExportSourceRef(TrainingExportSourceType.TrainingSample, "sample-excluded"),
                TrainingExportExclusionReason.OriginQuarantine)]);
    }

    private static TrainingExportExecutionResult Execution(TrainingExportPlan plan)
        => new(
            plan.PlanId,
            plan.PlanId,
            TrainingExportExecutionStatus.Created,
            1,
            1,
            0,
            plan.Classes.Count,
            @"C:\dataset",
            @"C:\dataset\data.yaml",
            @"C:\dataset\manifest.json",
            [plan.Images[0].ImageSha256]);
}
