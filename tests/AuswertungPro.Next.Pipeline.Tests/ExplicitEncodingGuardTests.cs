using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ExplicitEncodingGuardTests
{
    [Fact]
    public void Settings_and_log_file_io_use_explicit_encoding()
    {
        var root = FindSolutionRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "App.xaml.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "AppSettings.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Logging", "FileLoggerProvider.cs"),
        };

        var violations = FindImplicitEncodingViolations(root, files);

        Assert.True(
            violations.Count == 0,
            "File text IO in settings/logging code must specify Encoding explicitly:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Json_stores_and_catalogs_use_explicit_encoding()
    {
        var root = FindSolutionRoot();
        var files = new[]
        {
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Services", "ProtocolTrainingStore.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Services", "PresetCatalogStore.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Services", "DropdownOptionsStore.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Services", "CodeCatalog", "JsonCodeCatalogProvider.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Ai", "Training", "TrainingSampleGenerator.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "Ai", "Training", "Services", "PdfProtocolExtractor.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Protocol", "ProtocolAiServices.cs"),
            Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"),
        };

        var violations = FindImplicitEncodingViolations(root, files);

        Assert.True(
            violations.Count == 0,
            "JSON/catalog/log text IO must specify Encoding explicitly:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static List<string> FindImplicitEncodingViolations(string root, IReadOnlyList<string> files)
    {
        var violations = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!Regex.IsMatch(line, @"File\.(ReadAllText|WriteAllText|AppendAllText)\("))
                    continue;

                var statement = line;
                for (var j = i + 1; j < lines.Length && !statement.Contains(';'); j++)
                    statement += " " + lines[j].Trim();

                if (statement.Contains("Encoding.", StringComparison.Ordinal))
                    continue;

                violations.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {line.Trim()}");
            }
        }

        return violations;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found (AuswertungPro.sln).");
    }
}
