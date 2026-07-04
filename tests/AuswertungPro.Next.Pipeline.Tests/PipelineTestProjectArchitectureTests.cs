using System;
using System.IO;
using System.Xml.Linq;
using static AuswertungPro.Next.Pipeline.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class PipelineTestProjectArchitectureTests
{
    [Fact]
    public void PipelineTests_do_not_reference_ui_project_or_wpf()
    {
        var project = XDocument.Load(RepoFile(
            "tests",
            "AuswertungPro.Next.Pipeline.Tests",
            "AuswertungPro.Next.Pipeline.Tests.csproj"));

        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty);
        Assert.All(projectReferences, include =>
            Assert.False(
                include.Contains("AuswertungPro.Next.UI", StringComparison.OrdinalIgnoreCase),
                $"Pipeline-Tests duerfen das UI-Projekt nicht referenzieren: {include}"));

        var useWpfValues = project.Descendants("UseWPF").Select(e => e.Value.Trim());
        Assert.All(useWpfValues, value =>
            Assert.False(
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
                "Pipeline-Tests duerfen kein WPF-Testprojekt sein."));

        var targetFrameworkValues = project
            .Descendants()
            .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .Select(e => e.Value.Trim());
        Assert.All(targetFrameworkValues, value =>
            Assert.False(
                value.Contains("windows", StringComparison.OrdinalIgnoreCase),
                $"Pipeline-Tests duerfen kein Windows-Zielframework verwenden: {value}"));
    }
}
