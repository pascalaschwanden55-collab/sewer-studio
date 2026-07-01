using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SourceTextArchitectureHygieneTests
{
    [Theory]
    [InlineData("BuilderPageHoldingDataLineBuilderTests.cs")]
    [InlineData("BuilderPagePdfBlockBuilderTests.cs")]
    [InlineData("BuilderPageRowFilterTests.cs")]
    [InlineData("BuilderPageSpecialStatsCalculatorTests.cs")]
    [InlineData("BuilderPageSummaryEntryBuilderTests.cs")]
    [InlineData("BuilderPageViewModelThreadingTests.cs")]
    [InlineData("CostCalculatorCatalogFilterArchitectureTests.cs")]
    [InlineData("CostCalculatorImportDefaultsArchitectureTests.cs")]
    [InlineData("CostCalculatorLineOrderArchitectureTests.cs")]
    [InlineData("CostCalculatorLineSuggestionArchitectureTests.cs")]
    [InlineData("CostCalculatorMeasureInputArchitectureTests.cs")]
    [InlineData("CostCalculatorMeasureSelectionArchitectureTests.cs")]
    [InlineData("CostCalculatorPdfExportModelBuilderTests.cs")]
    [InlineData("DataPageAutoSaveArchitectureTests.cs")]
    [InlineData("DataPageCommandTargetControllerTests.cs")]
    [InlineData("DataPageCostRestoreArchitectureTests.cs")]
    [InlineData("DataPageDragStartPolicyTests.cs")]
    [InlineData("DataPageDropReorderControllerTests.cs")]
    [InlineData("DataPageMediaSearchArchitectureTests.cs")]
    [InlineData("DataPageMeasureSuggestionArchitectureTests.cs")]
    [InlineData("DataPageOriginalPdfArchitectureTests.cs")]
    [InlineData("DataPagePrintArchitectureTests.cs")]
    [InlineData("DataPageProtocolMediaLinkArchitectureTests.cs")]
    [InlineData("DataPageProtocolWindowArchitectureTests.cs")]
    [InlineData("DataPageRecordCollectionArchitectureTests.cs")]
    [InlineData("DataPageRecordCommandRouterTests.cs")]
    [InlineData("DataPageRowNavigationControllerTests.cs")]
    [InlineData("DataPageSanierungWindowArchitectureTests.cs")]
    [InlineData("DataPageSelectionChangedControllerTests.cs")]
    [InlineData("DataPageToolbarLayoutTests.cs")]
    [InlineData("DataPageVideoAnalysisArchitectureTests.cs")]
    [InlineData("DataPageVideoPlaybackArchitectureTests.cs")]
    [InlineData("DataPageVideoPathArchitectureTests.cs")]
    [InlineData("DataPageVideoRelinkArchitectureTests.cs")]
    [InlineData("DataGridWrappingTextColumnFactoryTests.cs")]
    [InlineData("TrainingCenterBatchImportThreadingTests.cs")]
    [InlineData("TrainingCenterSelfTrainingArchitectureTests.cs")]
    [InlineData("TrainingCenterUiThreadArchitectureTests.cs")]
    [InlineData("TrainingCenterPersistenceGuardTests.cs")]
    [InlineData("TrainingCenterReviewCodeExplorerTests.cs")]
    [InlineData("TrainingCenterReviewSamPersistenceTests.cs")]
    [InlineData("TrainingCenterReviewThreadingTests.cs")]
    [InlineData("TrainingFfmpegPathResolverTests.cs")]
    [InlineData("VideoLabelToolCodeBrowserTests.cs")]
    [InlineData("VideoLabelToolSelectionTests.cs")]
    [InlineData("VideoLabelToolServerSecurityTests.cs")]
    [InlineData("VideoLabelToolVisualStyleTests.cs")]
    [InlineData("VsaCodeExplorerWindowDispatcherTests.cs")]
    public void Focused_architecture_tests_use_shared_source_text_helpers(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(
            SourceTextTestHelpers.FindRepositoryRoot(),
            "tests",
            "AuswertungPro.Next.UI.Tests",
            fileName));

        Assert.DoesNotContain("private static string FindRepositoryRoot", source);
        Assert.DoesNotContain("private static string FindRepoRoot", source);
        Assert.DoesNotContain("private static string FindRepoFile", source);
        Assert.DoesNotContain("private static string RepoFile", source);
        Assert.DoesNotContain("private static string ExtractMethod(", source);
        Assert.DoesNotContain("private static string ExtractMethodBody", source);
    }
}
