using System.IO;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicPersistenceArchitectureTests
{
    public static TheoryData<string> DirectOverwriteTargets => new()
    {
        Path.Combine("src", "AuswertungPro.Next.Application", "Protocol", "JsonCodeCatalogProvider.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Training", "StageAExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Import", "ImportRunReportExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Common", "ProjectFileLocator.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "MeasureRecommendationService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Export", "CsvExcelExportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "HoldingFolderDistributor.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectFieldCsvExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Map", "NetworkGeometryCache.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "YoloDatasetExportService.cs"),
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
        Assert.DoesNotContain("File.WriteAllTextAsync(", source, System.StringComparison.Ordinal);
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
