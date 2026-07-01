using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SourceTextArchitectureHygieneTests
{
    [Fact]
    public void Layer_boundary_fitness_tests_live_in_fitness_suite()
    {
        var root = SourceTextTestHelpers.FindRepositoryRoot();
        var fitness = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "ArchitectureFitnessTests.cs"));
        var guard = File.ReadAllText(Path.Combine(root, "tests", "AuswertungPro.Next.UI.Tests", "UiArchitectureGuardTests.cs"));

        const string layerBoundaryTest = "PlayerWindow_partials_do_not_import_ui_services_namespace";

        Assert.Contains($"public void {layerBoundaryTest}", fitness);
        Assert.DoesNotContain($"public void {layerBoundaryTest}", guard);
    }

    [Theory]
    [InlineData("BuilderPageHoldingDataLineBuilderTests.cs")]
    [InlineData("BuilderPagePdfBlockBuilderTests.cs")]
    [InlineData("BuilderPageRowFilterTests.cs")]
    [InlineData("BuilderPageSpecialStatsCalculatorTests.cs")]
    [InlineData("BuilderPageSummaryEntryBuilderTests.cs")]
    [InlineData("BuilderPageViewModelThreadingTests.cs")]
    [InlineData("AiStartupUiTests.cs")]
    [InlineData("ArchitectureFitnessTests.cs")]
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
    [InlineData("DataGridHorizontalAlignmentToTextAlignmentConverterTests.cs")]
    [InlineData("DesignAuditChromeAndGlyphTests.cs")]
    [InlineData("DesignAuditDialogMigrationTests.cs")]
    [InlineData("DesignAuditPlayerCodingSidePanelTests.cs")]
    [InlineData("DesignAuditThemeResourceTests.cs")]
    [InlineData("GridDockingControllerTests.cs")]
    [InlineData("ImportArchitectureGuardTests.cs")]
    [InlineData("PageViewModelLifecycleTests.cs")]
    [InlineData("PlayerWindowResourceDictionaryTests.cs")]
    [InlineData("ProjektEroeffnungShellGuardTests.cs")]
    [InlineData("SchaechtePageColumnLayoutRefactorTests.cs")]
    [InlineData("ShellNavigationPolicyTests.cs")]
    [InlineData("SystemMonitorProcessSafetyTests.cs")]
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
    [InlineData("VsaCodeExplorerCollectionDispatchTests.cs")]
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
        Assert.DoesNotContain("internal static string RepoFile", source);
        Assert.DoesNotContain("private static string ExtractMethod(", source);
        Assert.DoesNotContain("private static string ExtractMethodBody", source);
    }
}
