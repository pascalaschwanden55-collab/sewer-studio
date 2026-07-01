using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SourceTextArchitectureHygieneTests
{
    [Theory]
    [InlineData("CostCalculatorCatalogFilterArchitectureTests.cs")]
    [InlineData("CostCalculatorImportDefaultsArchitectureTests.cs")]
    [InlineData("CostCalculatorLineOrderArchitectureTests.cs")]
    [InlineData("CostCalculatorLineSuggestionArchitectureTests.cs")]
    [InlineData("CostCalculatorMeasureInputArchitectureTests.cs")]
    [InlineData("CostCalculatorMeasureSelectionArchitectureTests.cs")]
    [InlineData("CostCalculatorPdfExportModelBuilderTests.cs")]
    [InlineData("DataPageAutoSaveArchitectureTests.cs")]
    [InlineData("DataPageCommandTargetControllerTests.cs")]
    [InlineData("DataPageMediaSearchArchitectureTests.cs")]
    [InlineData("DataPageOriginalPdfArchitectureTests.cs")]
    [InlineData("DataPageProtocolWindowArchitectureTests.cs")]
    [InlineData("DataPageSelectionChangedControllerTests.cs")]
    [InlineData("DataPageToolbarLayoutTests.cs")]
    [InlineData("DataPageVideoPlaybackArchitectureTests.cs")]
    [InlineData("DataPageVideoRelinkArchitectureTests.cs")]
    [InlineData("DataGridWrappingTextColumnFactoryTests.cs")]
    [InlineData("TrainingCenterBatchImportThreadingTests.cs")]
    [InlineData("TrainingCenterSelfTrainingArchitectureTests.cs")]
    [InlineData("TrainingCenterUiThreadArchitectureTests.cs")]
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
        Assert.DoesNotContain("private static string RepoFile", source);
        Assert.DoesNotContain("private static string ExtractMethod(", source);
        Assert.DoesNotContain("private static string ExtractMethodBody", source);
    }
}
