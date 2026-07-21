using System.IO;
using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicPersistenceArchitectureTests
{
    public static TheoryData<string> DirectOverwriteTargets => new()
    {
        Path.Combine("src", "AuswertungPro.Next.Application", "Protocol", "JsonCodeCatalogProvider.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Evaluation", "YoloDetectBaselineScorer.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Evaluation", "EvalSetBenchmarkScorer.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Evaluation", "EvalSetManifestHasher.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Import", "ImportRunReportFileExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Common", "ProjectFileLocator.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "MeasureRecommendationService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Export", "CsvExcelExportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "HoldingFolderDistributor.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Import", "ImportSummaryExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Map", "HaltungCadastreTableFileStore.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Map", "XtfJsonGeometryCache.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "Services", "PdfProtocolExtractor.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "TrainingCenterImportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Teacher", "TrainingAnnotationExportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Teacher", "VsaYoloClassMapFileStore.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "SelfImproving", "ReviewQueueService.cs"),
        Path.Combine("src", "AuswertungPro.Next.UI", "Services", "KnowledgeBackupImportPostProcessor.cs")
    };

    [Theory]
    [MemberData(nameof(DirectOverwriteTargets))]
    public void PersistenteStores_verwenden_atomaren_TextWriter(string relativePath)
    {
        var source = File.ReadAllText(RepoFile(relativePath));

        AssertNoForbiddenTokens(
            source,
            "File.WriteAllText(",
            "File.WriteAllTextAsync(",
            "File.WriteAllLines(",
            "File.WriteAllLinesAsync(",
            "new StreamWriter(");
        Assert.True(
            source.Contains("AtomicTextFileWriter.WriteAllText", System.StringComparison.Ordinal)
            || source.Contains("AtomicTextFileWriter.Write(", System.StringComparison.Ordinal),
            "Persistente Textausgaben muessen ueber AtomicTextFileWriter laufen.");
    }

    [Fact]
    public void JsonProjectRepository_verwendetTempDateiUndAtomarenAustausch()
    {
        var source = File.ReadAllText(RepoFile(Path.Combine(
            "src", "AuswertungPro.Next.Infrastructure", "Projects", "JsonProjectRepository.cs")));

        Assert.Contains("Guid.NewGuid():N}.tmp", source, StringComparison.Ordinal);
        Assert.Contains("File.Replace(tempPath, fullPath, backupPath", source, StringComparison.Ordinal);
        Assert.Contains("File.Move(tempPath, fullPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllText(fullPath", source, StringComparison.Ordinal);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene direkte Schreib-APIs gefunden: " + string.Join(", ", hits));
    }
}
