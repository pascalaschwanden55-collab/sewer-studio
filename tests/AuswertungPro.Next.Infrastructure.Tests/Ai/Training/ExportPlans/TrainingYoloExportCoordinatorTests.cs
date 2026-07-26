using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingYoloExportCoordinatorTests
{
    [Fact]
    public async Task RunAsync_erstellt_Plan_vor_Ausfuehrung_und_persistiert_erst_nach_Bestaetigung()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sequence = new List<string>();
            var sample = EligibleSample("sample-a", framePath);
            var untouched = EligibleSample("sample-b", framePath);
            var bundle = Bundle(TrainingExportSourceType.TrainingSample, sample.SampleId);
            var store = new FakeSampleStore(() => sequence.Add("persist"));
            var execution = new FakeExecutionService(bundle, () => sequence.Add("execution"));
            var completion = new FakeCompletionService(
                new TrainingExportCompletionService(),
                () => sequence.Add("completion"));
            var progress = new ProgressCapture();
            var coordinator = CreateCoordinator(
                store,
                bundle,
                execution,
                completion,
                sequence,
                inventorySamples: [sample, untouched]);
            var timestamp = DateTimeOffset.Parse("2026-07-17T10:15:00+02:00");

            var result = await coordinator.RunAsync(
                Command([sample, untouched], timestamp),
                progress);

            Assert.Equal(TrainingYoloExportResultStatus.Completed, result.Status);
            Assert.Equal(1, result.Completion.MarkedTrainingSamples);
            Assert.Equal(CompletionTimestamp().UtcDateTime, sample.ExportedUtc);
            Assert.Null(untouched.ExportedUtc);
            Assert.Equal(1, store.MergeCalls);
            Assert.Equal(
                ["registry", "inventory", "class-map", "input", "plan", "execution", "completion", "persist"],
                sequence);
            Assert.Equal(
                [
                    TrainingYoloExportProgressStage.PreparingSamples,
                    TrainingYoloExportProgressStage.InspectingInventory,
                    TrainingYoloExportProgressStage.CreatingPlan,
                    TrainingYoloExportProgressStage.ExecutingPlan,
                    TrainingYoloExportProgressStage.Completing,
                    TrainingYoloExportProgressStage.Completed
                ],
                progress.Items.Select(item => item.Stage));
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_leerer_Plan_beendet_ohne_Ausfuehrung_und_Abschluss()
    {
        var bundle = Bundle(imageCount: 0);
        var execution = new FakeExecutionService(bundle);
        var completion = new FakeCompletionService(new TrainingExportCompletionService());
        var progress = new ProgressCapture();
        var coordinator = CreateCoordinator(
            new FakeSampleStore(),
            bundle,
            execution,
            completion);

        var result = await coordinator.RunAsync(Command([], Timestamp()), progress);

        Assert.Equal(TrainingYoloExportResultStatus.NoImages, result.Status);
        Assert.Null(result.Execution);
        Assert.Equal(0, result.Completion.MarkedTrainingSamples);
        Assert.Equal(0, execution.Calls);
        Assert.Equal(0, completion.Calls);
        Assert.Equal(TrainingYoloExportProgressStage.NoImages, progress.Items[^1].Stage);
    }

    [Fact]
    public async Task RunAsync_persistiert_geaenderte_Eligibility_erst_nach_bestaetigtem_Export()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sequence = new List<string>();
            var sample = WithCode(EligibleSample("sample-invalid", framePath), "ZZZ");
            var valid = EligibleSample("sample-valid", framePath);
            var store = new FakeSampleStore(() => sequence.Add("persist"));
            var input = new FakePlanInputBuilder(() => sequence.Add("input"));
            var bundle = Bundle(TrainingExportSourceType.TrainingSample, valid.SampleId);
            var coordinator = CreateCoordinator(
                store,
                bundle,
                new FakeExecutionService(bundle, () => sequence.Add("execution")),
                new FakeCompletionService(
                    new TrainingExportCompletionService(),
                    () => sequence.Add("completion")),
                sequence,
                input,
                [sample, valid]);

            await coordinator.RunAsync(Command([sample, valid], Timestamp()));

            Assert.False(sample.TrainingEligible);
            Assert.Equal(TrainingSampleEligibility.InvalidCatalogCodeReason, sample.TrainingEligibilityReason);
            Assert.Equal(1, store.MergeCalls);
            Assert.Equal(0, store.SaveCalls);
            Assert.Equal(["sample-valid"], input.ApprovedSampleIds);
            Assert.Equal(
                ["registry", "inventory", "class-map", "input", "plan", "execution", "completion", "persist"],
                sequence);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_PlanOnly_prueft_den_Plan_ohne_Mutation_oder_Schreibzugriff()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sample = EligibleSample("sample-plan", framePath);
            sample.TrainingEligible = false;
            sample.TrainingEligibilityReason = "vorheriger Wert";
            var bundle = Bundle(TrainingExportSourceType.TrainingSample, sample.SampleId);
            var store = new FakeSampleStore();
            var execution = new FakeExecutionService(bundle);
            var completion = new FakeCompletionService(new TrainingExportCompletionService());
            var progress = new ProgressCapture();
            var coordinator = CreateCoordinator(
                store,
                bundle,
                execution,
                completion,
                inventorySamples: [sample]);

            var result = await coordinator.RunAsync(
                Command([sample], Timestamp()) with { Mode = TrainingYoloExportMode.PlanOnly },
                progress);

            Assert.Equal(TrainingYoloExportResultStatus.Planned, result.Status);
            Assert.Null(result.Execution);
            Assert.Equal(0, result.Completion.MarkedTrainingSamples);
            Assert.False(sample.TrainingEligible);
            Assert.Equal("vorheriger Wert", sample.TrainingEligibilityReason);
            Assert.Null(sample.ExportedUtc);
            Assert.Equal(0, store.MergeCalls);
            Assert.Equal(0, store.SaveCalls);
            Assert.Equal(0, execution.Calls);
            Assert.Equal(0, completion.Calls);
            Assert.Equal(TrainingYoloExportProgressStage.Planned, progress.Items[^1].Stage);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_UI_Liste_beeinflusst_die_Plankandidaten_nicht()
    {
        var framePath = CreateFrameFile();
        try
        {
            var inventorySample = EligibleSample("sample-inventory", framePath);
            var uiOnlySample = EligibleSample("sample-ui-alt", framePath);
            var input = new FakePlanInputBuilder(() => { });
            var bundle = Bundle(imageCount: 0);
            var coordinator = CreateCoordinator(
                new FakeSampleStore(),
                bundle,
                new FakeExecutionService(bundle),
                new FakeCompletionService(new TrainingExportCompletionService()),
                inputBuilder: input,
                inventorySamples: [inventorySample]);

            await coordinator.RunAsync(Command([uiOnlySample], Timestamp()));

            Assert.Equal(["sample-inventory"], input.ApprovedSampleIds);
            Assert.DoesNotContain("sample-ui-alt", input.ApprovedSampleIds!);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_schliesst_nicht_manuelles_Sample_aus()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sample = EligibleSample("sample-auto", framePath);
            sample.SourceType = SourceTypeNames.BatchImport;
            var input = new FakePlanInputBuilder(() => { });
            var bundle = Bundle(imageCount: 0);
            var coordinator = CreateCoordinator(
                new FakeSampleStore(),
                bundle,
                new FakeExecutionService(bundle),
                new FakeCompletionService(new TrainingExportCompletionService()),
                inputBuilder: input,
                inventorySamples: [sample]);

            await coordinator.RunAsync(Command([sample], Timestamp()));

            Assert.Empty(input.ApprovedSampleIds!);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_schliesst_fremd_bestaetigtes_Sample_aus()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sample = EligibleSample("sample-fremd", framePath);
            sample.ConfirmedByUser = "Andere Person";
            var input = new FakePlanInputBuilder(() => { });
            var bundle = Bundle(imageCount: 0);
            var coordinator = CreateCoordinator(
                new FakeSampleStore(),
                bundle,
                new FakeExecutionService(bundle),
                new FakeCompletionService(new TrainingExportCompletionService()),
                inputBuilder: input,
                inventorySamples: [sample]);

            await coordinator.RunAsync(Command([sample], Timestamp()));

            Assert.Empty(input.ApprovedSampleIds!);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_Pilotregister_begrenzt_den_Live_Snapshot_auf_freigegebene_Samples()
    {
        var framePath = CreateFrameFile();
        try
        {
            var selected = EligibleSample("sample-pilot", framePath);
            var notSelected = EligibleSample("sample-ausserhalb", framePath);
            var input = new FakePlanInputBuilder(() => { });
            var bundle = Bundle(imageCount: 0);
            var coordinator = CreateCoordinator(
                new FakeSampleStore(),
                bundle,
                new FakeExecutionService(bundle),
                new FakeCompletionService(new TrainingExportCompletionService()),
                inputBuilder: input,
                inventorySamples: [selected, notSelected],
            registrySampleIds: new HashSet<string>(
                ["sample-pilot"],
                StringComparer.OrdinalIgnoreCase));

            await coordinator.RunAsync(Command([selected, notSelected], Timestamp()));

            Assert.Equal(["sample-pilot"], input.ApprovedSampleIds);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    [Fact]
    public async Task RunAsync_Ausfuehrungsfehler_veraendert_und_speichert_keine_Samples()
    {
        var framePath = CreateFrameFile();
        try
        {
            var sample = WithCode(EligibleSample("sample-invalid", framePath), "ZZZ");
            var valid = EligibleSample("sample-valid", framePath);
            sample.TrainingEligible = true;
            sample.TrainingEligibilityReason = "vorher";
            var bundle = Bundle(TrainingExportSourceType.TrainingSample, valid.SampleId);
            var store = new FakeSampleStore();
            var completion = new FakeCompletionService(new TrainingExportCompletionService());
            var coordinator = CreateCoordinator(
                store,
                bundle,
                new FakeExecutionService(
                    bundle,
                    error: new InvalidOperationException("Export abgebrochen")),
                completion,
                inventorySamples: [sample, valid]);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.RunAsync(Command([sample, valid], Timestamp())));

            Assert.True(sample.TrainingEligible);
            Assert.Equal("vorher", sample.TrainingEligibilityReason);
            Assert.Null(sample.ExportedUtc);
            Assert.Equal(0, store.MergeCalls);
            Assert.Equal(0, completion.Calls);
        }
        finally
        {
            File.Delete(framePath);
        }
    }

    private static TrainingYoloExportCoordinator CreateCoordinator(
        FakeSampleStore store,
        TrainingExportPlanBundle bundle,
        FakeExecutionService execution,
        FakeCompletionService completion,
        List<string>? sequence = null,
        FakePlanInputBuilder? inputBuilder = null,
        IReadOnlyList<TrainingSample>? inventorySamples = null,
        IReadOnlySet<string>? registrySampleIds = null)
    {
        var events = sequence ?? [];
        return new TrainingYoloExportCoordinator(
            Path.Combine(Path.GetTempPath(), "SewerStudioTests", "knowledge"),
            Path.Combine(Path.GetTempPath(), "SewerStudioTests", "eval"),
            store,
            new FakeCodeCatalog(["BAB"]),
            new FakeRegistryStore(
                () => events.Add("registry"),
                RegistryBundle(registrySampleIds)),
            new FakeInventoryService(
                () => events.Add("inventory"),
                inventorySamples ?? []),
            new FakeClassMapStore(() => events.Add("class-map")),
            inputBuilder ?? new FakePlanInputBuilder(() => events.Add("input")),
            new FakePlanService(bundle, () => events.Add("plan")),
            execution,
            completion,
            new FixedTimeProvider(CompletionTimestamp()));
    }

    private static TrainingYoloExportCommand Command(
        IReadOnlyList<TrainingSample> samples,
        DateTimeOffset timestamp)
        => new(timestamp, UpdateTargets: samples);

    private static DateTimeOffset Timestamp()
        => DateTimeOffset.Parse("2026-07-17T08:00:00Z");

    private static DateTimeOffset CompletionTimestamp()
        => DateTimeOffset.Parse("2026-07-17T08:30:00Z");

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

    private static TrainingExportExecutionResult Execution(TrainingExportPlan plan)
        => new(
            plan.PlanId,
            plan.PlanId,
            TrainingExportExecutionStatus.Created,
            plan.Images.Count,
            plan.Images.Count,
            0,
            plan.Classes.Count,
            @"C:\dataset",
            @"C:\dataset\data.yaml",
            @"C:\dataset\manifest.json",
            plan.Images.Select(image => image.ImageSha256).ToArray());

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

    private sealed class FakeSampleStore(Action? onMerge = null) : ITrainingSampleStore
    {
        public int SaveCalls { get; private set; }
        public int MergeCalls { get; private set; }

        public Task<List<TrainingSample>> LoadAsync() => Task.FromResult<List<TrainingSample>>([]);

        public Task SaveAsync(List<TrainingSample> samples)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
        {
            MergeCalls++;
            onMerge?.Invoke();
            return Task.CompletedTask;
        }

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

    private sealed class FakeRegistryStore(
        Action onRead,
        TrainingExportRegistryBundle? bundle = null) : ITrainingExportRegistryStore
    {
        public TrainingExportRegistryBundle ReadBundle()
        {
            onRead();
            return bundle ?? RegistryBundle();
        }
    }

    private sealed class FakeInventoryService(
        Action onInspect,
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
        {
            onInspect();
            return Task.FromResult(InventorySnapshot(trainingSamples));
        }
    }

    private sealed class FakeClassMapStore(Action onRead) : ITrainingYoloClassMapStore
    {
        public TrainingYoloClassMapSnapshot ReadSnapshot()
        {
            onRead();
            return new TrainingYoloClassMapSnapshot(
                2,
                new string('a', 64),
                YoloDetectClassMapV2.Classes,
                []);
        }
    }

    private sealed class FakePlanInputBuilder(Action onBuild) : ITrainingExportPlanInputBuilder
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
            onBuild();
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

    private sealed class FakePlanService(
        TrainingExportPlanBundle bundle,
        Action onCreate) : ITrainingExportPlanService
    {
        public TrainingExportPlanBundle CreatePlan(TrainingExportPlanRequest request)
        {
            onCreate();
            return bundle;
        }
    }

    private sealed class FakeExecutionService(
        TrainingExportPlanBundle expectedBundle,
        Action? onExecute = null,
        Exception? error = null) : ITrainingExportExecutionService
    {
        public int Calls { get; private set; }

        public Task<TrainingExportExecutionOutcome> ExecuteAsync(
            TrainingExportPlanBundle bundle,
            CancellationToken cancellationToken = default)
        {
            Assert.Same(expectedBundle, bundle);
            Calls++;
            onExecute?.Invoke();
            if (error is not null)
                return Task.FromException<TrainingExportExecutionOutcome>(error);
            return Task.FromResult(new TrainingExportExecutionOutcome(
                TrainingExportExecutionRoute.Sidecar,
                Execution(bundle.Plan),
                "1.0"));
        }
    }

    private sealed class FakeCompletionService(
        ITrainingExportCompletionService inner,
        Action? onApply = null) : ITrainingExportCompletionService
    {
        public int Calls { get; private set; }

        public TrainingExportCompletionResult Apply(
            TrainingExportPlan plan,
            TrainingExportExecutionResult execution,
            IReadOnlyList<TrainingSample> samples,
            DateTime exportedUtc)
        {
            Calls++;
            onApply?.Invoke();
            return inner.Apply(plan, execution, samples, exportedUtc);
        }
    }
}
