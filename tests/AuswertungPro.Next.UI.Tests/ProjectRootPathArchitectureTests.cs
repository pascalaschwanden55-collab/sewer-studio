using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectRootPathArchitectureTests
{
    [Fact]
    public void Ui_project_json_paths_resolve_via_ProjectFileLocator()
    {
        var checkedFiles = new[]
        {
            "src/AuswertungPro.Next.UI/Ai/CodingProtocolPdfExportPlanner.cs",
            "src/AuswertungPro.Next.UI/Views/Windows/BeobachtungenWindow.xaml.cs",
            "src/AuswertungPro.Next.UI/DataPage/SchachtFileTargetResolver.cs"
        };

        var offenders = checkedFiles
            .SelectMany(file =>
            {
                var source = ReadRepoFile(file);
                var lines = source.Split(["\r\n", "\n"], StringSplitOptions.None);
                var problems = new List<string>();

                if (!source.Contains("ProjectFileLocator.ProjectRootFromFile(", StringComparison.Ordinal))
                    problems.Add($"{file}: nutzt ProjectFileLocator.ProjectRootFromFile(...) nicht");

                problems.AddRange(lines
                    .Where(line => line.Contains("Path.GetDirectoryName(lastProjectPath)", StringComparison.Ordinal)
                                   || line.Contains("System.IO.Path.GetDirectoryName(lastProjectPath)", StringComparison.Ordinal))
                    .Select(line => $"{file}: direkte Projektroot-Ableitung per {line.Trim()}"));

                return problems;
            })
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Projektdateien\\projekt.json darf nicht per GetDirectoryName als Projektroot verwendet werden. "
            + "Nutze ProjectFileLocator.ProjectRootFromFile:\n"
            + string.Join("\n", offenders));
    }

    private static string ReadRepoFile(string relativePath)
        => File.ReadAllText(RepoFile(relativePath.Split('/')));
}
