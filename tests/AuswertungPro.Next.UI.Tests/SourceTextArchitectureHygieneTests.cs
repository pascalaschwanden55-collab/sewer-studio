using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SourceTextArchitectureHygieneTests
{
    [Theory]
    [InlineData("DataPageCommandTargetControllerTests.cs")]
    [InlineData("DataGridWrappingTextColumnFactoryTests.cs")]
    [InlineData("TrainingCenterBatchImportThreadingTests.cs")]
    [InlineData("TrainingCenterSelfTrainingArchitectureTests.cs")]
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
        Assert.DoesNotContain("private static string ExtractMethodBody", source);
    }
}
