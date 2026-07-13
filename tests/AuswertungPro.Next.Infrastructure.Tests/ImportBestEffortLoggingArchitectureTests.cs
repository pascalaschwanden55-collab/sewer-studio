using System.IO;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ImportBestEffortLoggingArchitectureTests
{
    [Fact]
    public void SidecarXtf_enumeration_catches_log_and_continue()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "HoldingFolderDistributor.SidecarXtf.cs"));

        Assert.DoesNotContain("catch { }", source);
        Assert.Contains("Sidecar-Suche uebersprungen", source);
        Assert.Contains("BestEffort.ReportWarning", source);
    }

    [Fact]
    public void DichtheitImport_best_effort_catches_log_and_continue()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "DichtheitImportDistributor.cs"));

        Assert.DoesNotContain("catch {", source);
        Assert.Contains("Kandidat uebersprungen", source);
        Assert.Contains("Vorhandene DP-Groessen konnten nicht gelesen werden", source);
        Assert.Contains("BestEffort.ReportWarning", source);
    }
}
