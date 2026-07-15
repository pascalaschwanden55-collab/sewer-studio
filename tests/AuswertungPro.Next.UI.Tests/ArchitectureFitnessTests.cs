using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ArchitectureFitnessTests
{
    [Fact]
    public void Shell_view_model_has_no_dead_guide_code_when_xaml_has_no_guide_bindings()
    {
        var uiRoot = RepoFile("src", "AuswertungPro.Next.UI");
        var xamlOffenders = Directory.EnumerateFiles(uiRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(file =>
            {
                var relative = Path.GetRelativePath(uiRoot, file);
                var issues = new List<string>();
                if (File.ReadAllText(file).Contains("Guide", StringComparison.Ordinal))
                    issues.Add("Guide content");
                if (relative.Contains("Guide", StringComparison.Ordinal))
                    issues.Add("Guide path");
                return issues.Select(issue => $"{relative}: {issue}");
            });

        var shellOffenders = FindFileTokenOffenders(
            RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"),
            "Guide",
            "Ratten-Assistent");

        var offenders = xamlOffenders.Concat(shellOffenders).ToArray();

        Assert.True(
            offenders.Length == 0,
            "ShellViewModel soll keinen toten Guide-Code behalten, wenn XAML keine Guide-Bindings mehr hat:\n"
            + string.Join("\n", offenders));
    }

}
