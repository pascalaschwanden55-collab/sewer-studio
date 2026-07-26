using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

/// <summary>
/// Sichtbarkeit des dokumentierten Pilot-Gates: Ein gesetztes Freigaberegister
/// sperrt nicht registrierte Samples weiterhin still aus, vollstaendige
/// Goldsamples darunter werden aber gezaehlt und gemeldet.
/// </summary>
public sealed class TrainingYoloExportCoordinatorRegistryGateTests
{
    [Fact]
    public async Task RunAsync_Pilotregister_meldet_uebersprungene_exportfaehige_Goldsamples()
    {
        var framePath = CreateFrameFile();
        try
        {
            var registered = EligibleSample("sample-pilot", framePath);
            var skipped = EligibleSample("sample-offen", framePath);
            var incomplete = WithCode(EligibleSample("sample-unvollstaendig", framePath), "ZZZ");
            var input = new FakePlanInputBuilder();
            var bundle = Bundle(TrainingExportSourceType.TrainingSample, registered.SampleId);
            var progress = new ProgressCapture();
            var coordinator = CreateCoordinator(
                bundle,
                input,
                inventorySamples: [registered, skipped, incomplete],
                registrySampleIds: new HashSet<string>(
                    ["sample-pilot"],
                    StringComparer.OrdinalIgnoreCase));

            var result = await coordinator.RunAsync(
                Command([registered, skipped, incomplete], Timestamp()),
                progress);

            Assert.Equal(TrainingYoloExportResultStatus.Completed, result.Status);
            Assert.Equal(["sample-offen"], result.RegistryGateSkippedSampleIds);
            Assert.Equal(["sample-pilot"], input.ApprovedSampleIds);
            var notice = Assert.Single(
                progress.Items,
                item => item.Stage == TrainingYoloExportProgressStage.RegistryGateNotice);
            Assert.Contains("1 vollstaendige Goldsamples", notice.Message, StringComparison.Ordinal);
            Assert.Contains("nicht im Freigaberegister", notice.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_leeres_Register_erzeugt_keinen_Registerhinweis()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sample = EligibleSample("sample-a", framePath);
            var bundle = Bundle(imageCount: 0);
            var progress = new ProgressCapture();
            var coordinator = CreateCoordinator(
                bundle,
                new FakePlanInputBuilder(),
                inventorySamples: [sample],
                registrySampleIds: null);

            var result = await coordinator.RunAsync(Command([sample], Timestamp()), progress);

            Assert.Equal(TrainingYoloExportResultStatus.NoImages, result.Status);
            Assert.Null(result.RegistryGateSkippedSampleIds);
            Assert.DoesNotContain(
                progress.Items,
                item => item.Stage == TrainingYoloExportProgressStage.RegistryGateNotice);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    private static TrainingYoloExportCoordinator CreateCoordinator(
        TrainingExportPlanBundle bundle,
        FakePlanInputBuilder inputBuilder,
        IReadOnlyList<TrainingSample> inventorySamples,
        IReadOnlySet<string>? registrySampleIds)
        => new(
            Path.Combine(Path.GetTempPath(), "SewerStudioTests", "knowledge"),
            Path.Combine(Path.GetTempPath(), "SewerStudioTests", "eval"),
            new FakeSampleStore(),
            new FakeCodeCatalog(["BAB"]),
            new FakeRegistryStore(RegistryBundle(registrySampleIds)),
            new FakeInventoryService(inventorySamples),
            new FakeClassMapStore(),
            inputBuilder,
            new FakePlanService(bundle),
            new FakeExecutionService(bundle),
            new FakeCompletionService(new TrainingExportCompletionService()),
            new FixedTimeProvider(Timestamp()));

    private static TrainingYoloExportCommand Command(
        IReadOnlyList<TrainingSample> samples,
        DateTimeOffset timestamp)
        => new(timestamp, UpdateTargets: samples);

    private static DateTimeOffset Timestamp()
        => DateTimeOffset.Parse("2026-07-17T08:00:00Z");

    private static string CreateFrameFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    private static TrainingSample EligibleSample(string id, string framePath)
        => new()
        {
            SampleId = id,
            Status = TrainingSampleStatus.Approved,
            FramePath = framePath,
            Code = "BAB",
            InspectionDate = new DateTime(2026, 7, 1),
            TrainingEligible = true,
            SourceType = SourceTypeNames.ManualCoding,
            HumanConfirmed = true,
            Corrected = false,
            ConfirmedByUser = "Test User",
            ConfirmedAtUtc = Timestamp().UtcDateTime,
            MatchLevel = MatchLevelNames.ReviewApproved,
            BboxXCenter = 0.5,
            BboxYCenter = 0.5,
            BboxWidth = 0.2,
            BboxHeight = 0.1,
            SamMaskRle = "0,4050,1,3949",
            SamMaskImageWidth = 100,
            SamMaskImageHeight = 80
        };

    private static TrainingSample WithCode(TrainingSample sample, string code)
    {
        sample.Code = code;
        return sample;
    }

    private static TrainingExportPlanBundle Bundle(
        TrainingExportSourceType sourceType = TrainingExportSourceType.TrainingSample,
        string sourceId = "sample-a",
        int imageCount = 1)
    {
        var classes = YoloDetectClassMapV2.Classes
            .OrderBy(item => item.Value)
            .Select(item => item.Key)
            .ToArray();
        var images = Enumerable.Range(0, imageCount)
            .Select(index =>
            {
                var hash = index.ToString("x64");
                return new TrainingExportPlannedImage(
                    hash,
                    "100-200",
                    TrainingExportTarget.Train,
                    $"img_{hash}.png",
                    [new TrainingExportPlannedLabel(
                        1,
                        "BAB_riss",
                        new TrainingExportBoundingBox(0.5, 0.5, 0.2, 0.1),
                        [new TrainingExportSourceRef(
                            sourceType,
                            imageCount == 1 ? sourceId : $"{sourceId}-{index}")])]);
            })
            .ToArray();
        var plan = new TrainingExportPlan(
            TrainingExportPlan.CurrentSchemaVersion,
            new string('a', 64),
            Timestamp(),
            "inventory-run",
            new Dictionary<string, string>
            {
                ["teacher_annotations.json"] = new string('b', 64),
                ["training_samples.json"] = new string('c', 64)
            },
            2,
            new string('d', 64),
            new string('e', 64),
            [new TrainingExportProtectedSetReference(
                "dev-val-v1",
                TrainingExportProtectedSetRole.DevelopmentValidation,
                new string('f', 64))],
            classes,
            ["100-200"],
            [],
            imageCount == 0
                ? new Dictionary<string, int>()
                : new Dictionary<string, int> { ["BAB_riss"] = imageCount },
            images,
            []);
        return new TrainingExportPlanBundle(
            plan,
            images.ToDictionary(image => image.ImageSha256, _ => @"C:\frame.png"));
    }

    private static TrainingExportRegistryBundle RegistryBundle(
        IReadOnlySet<string>? approvedSampleIds = null)
        => new(
            new TrainingExportRegistrySnapshot(
                TrainingExportRegistrySnapshot.CurrentSchemaVersion,
                new string('a', 64),
                TrainingExportRegistryApprovalStatus.Approved,
                "Test User",
                Timestamp(),
                new Dictionary<string, TrainingExportHoldingRole>
                {
                    ["100-200"] = TrainingExportHoldingRole.Train
                },
                [new TrainingExportProtectedSetReference(
                    "dev-val-v1",
                    TrainingExportProtectedSetRole.DevelopmentValidation,
                    new string('b', 64))])
            {
                ApprovedSampleIds = approvedSampleIds
                                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            },
            new Dictionary<string, string>
            {
                ["dev-val-v1"] = Path.Combine(Path.GetTempPath(), "SewerStudioTests", "eval")
            });

    private static TrainingDataInventoryRuntimeSnapshot InventorySnapshot(
        IReadOnlyList<TrainingSample> trainingSamples)
    {
        var status = new TrainingInventoryEvalProtectionStatus
        {
            ImageHashCheckEnabled = true,
            Sets =
            [
                new TrainingInventoryEvalSetStatus
                {
                    RootPath = Path.Combine(Path.GetTempPath(), "SewerStudioTests", "eval"),
                    ImageFiles = 1,
                    ManifestImageHashes = 1,
                    VerifiedImageHashes = 1,
                    HoldingKeys = 1,
                    ImageHashesComplete = true,
                    HoldingKeysComplete = true
                }
            ]
        };
        return new TrainingDataInventoryRuntimeSnapshot(
            new TrainingDataInventoryReport
            {
                KnowledgeRoot = Path.Combine(Path.GetTempPath(), "SewerStudioTests", "knowledge"),
                RunId = "inventory-run",
                GeneratedUtc = Timestamp(),
                EvalProtection = status
            },
            [],
            trainingSamples,
            new TrainingInventoryProtectionSnapshot(
                status,
                new HashSet<string>(),
                new HashSet<string>(),
                [new TrainingInventoryProtectedSetSnapshot(
                    "dev-val-v1",
                    Path.Combine(Path.GetTempPath(), "SewerStudioTests", "eval"),
                    new string('b', 64))],
                new string('c', 64)));
    }

    private sealed class ProgressCapture : IProgress<TrainingYoloExportProgress>
    {
        public List<TrainingYoloExportProgress> Items { get; } = [];

        public void Report(TrainingYoloExportProgress value) => Items.Add(value);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeSampleStore : ITrainingSampleStore
    {
        public Task<List<TrainingSample>> LoadAsync() => Task.FromResult<List<TrainingSample>>([]);

        public Task SaveAsync(List<TrainingSample> samples) => Task.CompletedTask;

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) => Task.CompletedTask;

        public Task MergeAndSaveAsync(List<TrainingSample> samples) => Task.CompletedTask;
        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RemoveBySampleIdAsync(string sampleId) => Task.FromResult(false);
        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample) => Task.FromResult(false);
    }

    private sealed class FakeCodeCatalog(IReadOnlyList<string> validCodes) : ICodeCatalogProvider
    {
        public IReadOnlyList<CodeDefinition> GetAll()
            => validCodes.Select(code => new CodeDefinition { Code = code }).ToArray();

        public bool TryGet(string code, out CodeDefinition def)
        {
            var found = validCodes.Any(item => item.Equals(code, StringComparison.OrdinalIgnoreCase));
            def = new CodeDefinition { Code = code, IsSelectable = found };
            return found;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();

        public IReadOnlyList<string> AllowedCodes() => validCodes;

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }

    private sealed class FakeRegistryStore(TrainingExportRegistryBundle bundle) : ITrainingExportRegistryStore
    {
        public TrainingExportRegistryBundle ReadBundle() => bundle;
    }

    private sealed class FakeInventoryService(
        IReadOnlyList<TrainingSample> trainingSamples) : ITrainingDataInventoryService
    {
        public Task<TrainingDataInventoryReport> InspectAsync(
            TrainingDataInventoryRequest request,
            IProgress<TrainingDataInventoryProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TrainingDataInventoryRuntimeSnapshot> InspectRuntimeSnapshotAsync(
            TrainingDataInventoryRequest request,
            IProgress<TrainingDataInventoryProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(InventorySnapshot(trainingSamples));
    }

    private sealed class FakeClassMapStore : ITrainingYoloClassMapStore
    {
        public TrainingYoloClassMapSnapshot ReadSnapshot()
            => new(
                2,
                new string('a', 64),
                YoloDetectClassMapV2.Classes,
                []);
    }

    private sealed class FakePlanInputBuilder : ITrainingExportPlanInputBuilder
    {
        public IReadOnlySet<string>? ApprovedSampleIds { get; private set; }

        public Task<TrainingExportPlanRequest> BuildAsync(
            TrainingDataInventoryRuntimeSnapshot inventory,
            TrainingExportRegistrySnapshot registry,
            IReadOnlySet<string> approvedTrainingSampleIds,
            TrainingYoloClassMapSnapshot classMap,
            DateTimeOffset generatedUtc,
            CancellationToken cancellationToken = default)
        {
            ApprovedSampleIds = new HashSet<string>(
                approvedTrainingSampleIds,
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new TrainingExportPlanRequest(
                [],
                classMap,
                registry,
                inventory.Report.RunId,
                new Dictionary<string, string> { ["source"] = new string('a', 64) },
                true,
                new HashSet<string>(),
                new HashSet<string>(),
                generatedUtc));
        }
    }

    private sealed class FakePlanService(TrainingExportPlanBundle bundle) : ITrainingExportPlanService
    {
        public TrainingExportPlanBundle CreatePlan(TrainingExportPlanRequest request) => bundle;
    }

    private sealed class FakeExecutionService(
        TrainingExportPlanBundle expectedBundle) : ITrainingExportExecutionService
    {
        public Task<TrainingExportExecutionOutcome> ExecuteAsync(
            TrainingExportPlanBundle bundle,
            CancellationToken cancellationToken = default)
        {
            Assert.Same(expectedBundle, bundle);
            var result = new TrainingExportExecutionResult(
                bundle.Plan.PlanId,
                bundle.Plan.PlanId,
                TrainingExportExecutionStatus.Created,
                bundle.Plan.Images.Count,
                bundle.Plan.Images.Count,
                0,
                bundle.Plan.Classes.Count,
                @"C:\dataset",
                @"C:\dataset\data.yaml",
                @"C:\dataset\manifest.json",
                bundle.Plan.Images.Select(image => image.ImageSha256).ToArray());
            return Task.FromResult(new TrainingExportExecutionOutcome(
                TrainingExportExecutionRoute.Sidecar,
                result,
                "1.0"));
        }
    }

    private sealed class FakeCompletionService(
        ITrainingExportCompletionService inner) : ITrainingExportCompletionService
    {
        public TrainingExportCompletionResult Apply(
            TrainingExportPlan plan,
            TrainingExportExecutionResult execution,
            IReadOnlyList<TrainingSample> samples,
            DateTime exportedUtc)
            => inner.Apply(plan, execution, samples, exportedUtc);
    }
}
