using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class TrainingYoloExportRuntimeTests
{
    [Fact]
    public void CreateLocal_bindet_alle_Pfade_einmalig_ohne_Ordner_anzulegen()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "training-yolo-runtime-tests",
            Guid.NewGuid().ToString("N"),
            "knowledge");
        var eval = Path.Combine(Path.GetDirectoryName(root)!, "eval");

        var runtime = TrainingYoloExportRuntime.CreateLocal(
            new TrainingYoloExportRuntimeOptions(root, eval),
            new FakeSampleStore(),
            new FakeCodeCatalog(),
            new FakeClassMapStore(),
            TimeProvider.System);

        Assert.Equal(Path.GetFullPath(root), runtime.KnowledgeRoot);
        Assert.Equal(Path.GetFullPath(eval), runtime.EvalSetRoot);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "training", "datasets"), runtime.DatasetRoot);
        Assert.IsType<TrainingExportLocalExecutionService>(runtime.Execution);
        Assert.IsType<TrainingYoloExportCoordinator>(runtime.Coordinator);
        Assert.False(Directory.Exists(root));
        Assert.False(Directory.Exists(eval));
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

    private sealed class FakeCodeCatalog : ICodeCatalogProvider
    {
        public IReadOnlyList<CodeDefinition> GetAll() => [];
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = new CodeDefinition();
            return false;
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();
        public IReadOnlyList<string> AllowedCodes() => [];
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }

    private sealed class FakeClassMapStore : ITrainingYoloClassMapStore
    {
        public TrainingYoloClassMapSnapshot ReadSnapshot() => throw new NotSupportedException();
    }
}
