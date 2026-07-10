using System.IO;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingScriptPathTests
{
    [Fact]
    public void VsaClassifierScripts_do_not_reference_an_old_repository_folder()
    {
        var directory = TestRepoPaths.RepoFile("training", "vsa_classifier");
        var scripts = Directory.GetFiles(directory, "*.py", SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(scripts);
        foreach (var script in scripts)
        {
            var source = File.ReadAllText(script);
            Assert.DoesNotContain("Sewer-Studio_KI_4.4", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Shared_training_paths_are_derived_from_the_script_location()
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile(
            "training",
            "vsa_classifier",
            "repo_paths.py"));

        Assert.Contains("Path(__file__).resolve().parents[2]", source, StringComparison.Ordinal);
        Assert.Contains("BENCHMARK_REPORT_ROOT", source, StringComparison.Ordinal);
    }
}
