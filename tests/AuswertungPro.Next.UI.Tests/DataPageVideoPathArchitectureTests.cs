using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoPathArchitectureTests
{
    [Fact]
    public void EnsureVideoPath_delegates_video_fallback_workflow_to_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "EnsureVideoPath");

        Assert.Contains("DataPageVideoPathWorkflowController.Resolve(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new VideoSearchTool(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectFolder(\"Video-Ordner auswaehlen\"", method, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenFile(\"Video auswaehlen\"", method, StringComparison.Ordinal);
        Assert.DoesNotContain("resManual.Message", method, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string methodName)
    {
        var marker = "private string? " + methodName + "(";
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException($"Method {methodName} not found.");

        var openBrace = source.IndexOf('{', markerIndex);
        if (openBrace < 0)
            throw new InvalidOperationException($"Method {methodName} has no body.");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                continue;
            }

            if (source[i] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(markerIndex, i - markerIndex + 1);
        }

        throw new InvalidOperationException($"Method {methodName} body is incomplete.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
