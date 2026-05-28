using System;
using System.IO;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PipelineTestProjectArchitectureTests
{
    [Fact]
    public void PipelineTests_do_not_reference_ui_project_or_wpf()
    {
        var projectPath = FindProjectFile();
        var projectXml = File.ReadAllText(projectPath);

        Assert.DoesNotContain("AuswertungPro.Next.UI", projectXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<UseWPF>true</UseWPF>", projectXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("net10.0-windows", projectXml, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindProjectFile()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "tests",
                "AuswertungPro.Next.Pipeline.Tests",
                "AuswertungPro.Next.Pipeline.Tests.csproj");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("Pipeline-Testprojekt wurde nicht gefunden.");
    }
}
