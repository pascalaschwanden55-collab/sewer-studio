using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterPersistenceGuardTests
{
    [Fact]
    public void TrainingCenterViewModel_SavesStateOnlyThroughBuildState()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        Assert.Contains("private TrainingCenterState BuildState()", source);
        Assert.Contains("RootFolders = new List<string>(_rootFolders)", source);
        Assert.DoesNotContain("SaveAsync(new TrainingCenterState", source);
    }

    [Fact]
    public void TrainingCenterStore_SerializesSavesAndUsesUniqueTempFiles()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "Ai", "Training", "TrainingCenterStore.cs"));

        Assert.Contains("SemaphoreSlim", source);
        Assert.Contains("Guid.NewGuid()", source);
        Assert.DoesNotContain("StoreFilePath + \".tmp\"", source);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
