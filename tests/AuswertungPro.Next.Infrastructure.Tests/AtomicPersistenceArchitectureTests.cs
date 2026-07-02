using System.IO;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicPersistenceArchitectureTests
{
    public static TheoryData<string> DirectOverwriteTargets => new()
    {
        Path.Combine("src", "AuswertungPro.Next.Application", "Protocol", "JsonCodeCatalogProvider.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "MeasureRecommendationService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Map", "NetworkGeometryCache.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "TrainingCenterImportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Teacher", "VsaYoloClassMap.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "SelfImproving", "ReviewQueueService.cs")
    };

    [Theory]
    [MemberData(nameof(DirectOverwriteTargets))]
    public void PersistenteStores_verwenden_atomaren_TextWriter(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

        Assert.DoesNotContain("File.WriteAllText(", source, System.StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllLines(", source, System.StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllLinesAsync(", source, System.StringComparison.Ordinal);
        Assert.Contains("AtomicTextFileWriter.WriteAllText", source, System.StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln nicht gefunden.");
    }
}
