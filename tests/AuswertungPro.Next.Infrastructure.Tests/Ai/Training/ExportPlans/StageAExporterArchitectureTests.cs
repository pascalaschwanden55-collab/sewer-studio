namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.ExportPlans;

public sealed class StageAExporterArchitectureTests
{
    [Fact]
    public void Cli_nutzt_Infrastructure_aber_keine_UI_Referenz()
    {
        var project = File.ReadAllText(TestRepoPaths.RepoFile(
            "tools",
            "StageAExporter",
            "StageAExporter.csproj"));

        Assert.Contains("AuswertungPro.Next.Infrastructure.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("AuswertungPro.Next.UI.csproj", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_ist_nur_Fassade_vor_der_gemeinsamen_lokalen_Runtime()
    {
        var runner = File.ReadAllText(TestRepoPaths.RepoFile(
            "tools",
            "StageAExporter",
            "StageAExporterRunner.cs"));
        var root = TestRepoPaths.RepoRoot();

        Assert.Contains("TrainingYoloExportRuntime.CreateLocal", runner, StringComparison.Ordinal);
        Assert.Contains("TrainingYoloExportMode.PlanOnly", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingExportPlanService", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("StageAExportOptions", runner, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.Application",
            "Ai",
            "Training",
            "StageAExporter.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.Application",
            "Ai",
            "Training",
            "StageALabelFormatting.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Ai",
            "Training",
            "YoloDatasetExportService.cs")));
    }

    [Fact]
    public void Cli_ist_im_Vollbuild_aber_nicht_im_Entwicklungsfilter()
    {
        var solution = File.ReadAllText(TestRepoPaths.RepoFile("AuswertungPro.sln"));
        var developmentFilter = File.ReadAllText(TestRepoPaths.RepoFile("AuswertungPro.Dev.slnf"));

        Assert.Contains("tools\\StageAExporter\\StageAExporter.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("StageAExporter", developmentFilter, StringComparison.Ordinal);
    }
}
