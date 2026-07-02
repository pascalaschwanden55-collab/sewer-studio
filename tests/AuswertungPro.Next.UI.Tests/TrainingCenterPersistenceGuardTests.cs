using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterPersistenceGuardTests
{
    [Fact]
    public void TrainingCenterViewModel_SavesStateOnlyThroughBuildState()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        Assert.Contains("private TrainingCenterState BuildState()", source);
        Assert.Contains("TrainingCenterStateController.BuildState(Cases, _rootFolders, DateTime.UtcNow)", source);
        Assert.DoesNotContain("RootFolders = new List<string>(_rootFolders)", source);
        Assert.DoesNotContain("SaveAsync(new TrainingCenterState", source);
    }

    [Fact]
    public void TrainingCenterStore_SerializesSavesAndUsesUniqueTempFiles()
    {
        var source = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Ai", "Training", "TrainingCenterStore.cs"));

        Assert.Contains("SemaphoreSlim", source);
        Assert.Contains("Guid.NewGuid()", source);
        Assert.DoesNotContain("StoreFilePath + \".tmp\"", source);
    }

}
