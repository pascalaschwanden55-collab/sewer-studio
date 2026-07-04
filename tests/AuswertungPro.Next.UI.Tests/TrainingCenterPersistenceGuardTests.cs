using System.IO;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterPersistenceGuardTests
{
    [Fact]
    public async Task TrainingCenterViewModel_SaveCommandPersistsCasesAndRootFolders()
    {
        using var temp = new TempDir();
        var storePath = Path.Combine(temp.Path, "training_center.json");
        var store = new TrainingCenterStore(storePath);
        var vm = CreateViewModel(store);
        var rootFolders = GetRootFolders(vm);

        vm.Cases.Add(new TrainingCase { CaseId = "case-1", FolderPath = @"C:\Training\Case1" });
        rootFolders.Add(@"C:\Training\Root");

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = await store.LoadAsync();
        Assert.Equal("case-1", Assert.Single(saved.Cases).CaseId);
        Assert.Equal([@"C:\Training\Root"], saved.RootFolders);
        Assert.Equal("Gespeichert: 1 Fälle, 1 Ordner", vm.StatusText);
    }

    [Fact]
    public async Task TrainingCenterStore_SavesAtomicallyWithBackupAndNoTempLeftovers()
    {
        using var temp = new TempDir();
        var storePath = Path.Combine(temp.Path, "training_center.json");
        var store = new TrainingCenterStore(storePath);

        await store.SaveAsync(new TrainingCenterState
        {
            Cases = [new TrainingCase { CaseId = "first" }],
            RootFolders = [@"C:\Training\First"]
        });

        await store.SaveAsync(new TrainingCenterState
        {
            Cases = [new TrainingCase { CaseId = "second" }],
            RootFolders = [@"C:\Training\Second"]
        });

        var current = await store.LoadAsync();
        var backup = await new TrainingCenterStore(storePath + ".bak").LoadAsync();

        Assert.Equal("second", Assert.Single(current.Cases).CaseId);
        Assert.Equal([@"C:\Training\Second"], current.RootFolders);
        Assert.Equal("first", Assert.Single(backup.Cases).CaseId);
        Assert.Equal([@"C:\Training\First"], backup.RootFolders);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "training-center-persistence-" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private static TrainingCenterViewModel CreateViewModel(TrainingCenterStore store)
        => new(
            store,
            new TrainingCenterImportService(),
            codeCatalog: null,
            kbDiagnostics: new NoopKnowledgeBaseDiagnosticsRunner(),
            settings: null,
            uiThread: new ImmediateUiThread());

    private static List<string> GetRootFolders(TrainingCenterViewModel vm)
    {
        var field = typeof(TrainingCenterViewModel).GetField(
            "_rootFolders",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsType<List<string>>(field.GetValue(vm));
    }

    private sealed class ImmediateUiThread : IUiThread
    {
        public void Run(Action action) => action();
    }

    private sealed class NoopKnowledgeBaseDiagnosticsRunner : IKnowledgeBaseDiagnosticsRunner
    {
        public Task<KnowledgeBaseStatusReport> ReadStatusAsync(int topCodes = 20, CancellationToken ct = default)
            => Task.FromResult(new KnowledgeBaseStatusReport(0, 0, 0, 0, 0, null, []));

        public Task<KnowledgeBaseQualityReport> ReadQualityAsync(CancellationToken ct = default)
            => Task.FromResult(new KnowledgeBaseQualityReport("", 0, "", 0));

        public Task<KnowledgeBaseDiagnosticsSummary> ReadSummaryAsync(int topCodes = 12, CancellationToken ct = default)
            => Task.FromResult(new KnowledgeBaseDiagnosticsSummary(0, 0, 0, null, 0, "", []));
    }
}
