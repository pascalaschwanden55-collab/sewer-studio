using System.IO;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ReleasePackagingScriptTests
{
    [Fact]
    public void Release_script_builds_self_contained_app_and_copies_sidecar_contract()
    {
        var source = File.ReadAllText(TestRepoPaths.RepoFile(
            "tools",
            "Publish-SewerStudio.ps1"));

        Assert.Contains("--self-contained", source, StringComparison.Ordinal);
        Assert.Contains("RestoreLockedMode=true", source, StringComparison.Ordinal);
        Assert.Contains("coreclr.dll", source, StringComparison.Ordinal);
        Assert.Contains("start_sidecar.ps1", source, StringComparison.Ordinal);
        Assert.Contains("requirements-lock.txt", source, StringComparison.Ordinal);
        Assert.Contains("active.classifier.weights_path", source, StringComparison.Ordinal);
        Assert.Contains("classifier/$classifierFileName", source, StringComparison.Ordinal);
        Assert.Contains("UTF8Encoding($false)", source, StringComparison.Ordinal);
        Assert.Contains("ls-files --others --exclude-standard -- src sidecar/sidecar", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Copy-Item -LiteralPath $sidecarSource -Destination",
            source,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("training_export", source, StringComparison.OrdinalIgnoreCase);

        var project = File.ReadAllText(TestRepoPaths.RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "AuswertungPro.Next.UI.csproj"));
        Assert.Contains("<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>", project, StringComparison.Ordinal);
    }
}
