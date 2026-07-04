using System.IO;
using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicPersistenceArchitectureTests
{
    public static TheoryData<string> DirectOverwriteTargets => new()
    {
        Path.Combine("src", "AuswertungPro.Next.Application", "Protocol", "JsonCodeCatalogProvider.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Evaluation", "EvalSetBenchmark.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Evaluation", "EvalSetManifestHasher.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Ai", "Training", "StageAExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Import", "ImportRunReportExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Application", "Common", "ProjectFileLocator.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "MeasureRecommendationService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Export", "CsvExcelExportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "HoldingFolderDistributor.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Import", "ProjectFieldCsvExporter.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Map", "HaltungCadastreExtractor.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Map", "NetworkGeometryCache.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "YoloDatasetExportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "Services", "PdfProtocolExtractor.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "TrainingCenterImportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Teacher", "TrainingAnnotationExportService.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "Teacher", "VsaYoloClassMap.cs"),
        Path.Combine("src", "AuswertungPro.Next.Infrastructure", "Ai", "SelfImproving", "ReviewQueueService.cs")
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

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene direkte Schreib-APIs gefunden: " + string.Join(", ", hits));
    }
}
