using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ArchitectureFitnessTests
{
    [Fact]
    public void Ui_code_accesses_App_Services_only_at_composition_root()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Normalize(Path.Combine(uiRoot, "MainWindow.xaml.cs"))
        };

        var offenders = Directory.EnumerateFiles(uiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !allowedFiles.Contains(Normalize(path)))
            .Select(path => new
            {
                Path = path,
                Lines = File.ReadLines(path)
                    .Select((line, index) => new { Line = line, Number = index + 1 })
                    .Where(item => item.Line.Contains("App.Services", StringComparison.Ordinal))
                    .Select(item => item.Number)
                    .ToArray()
            })
            .Where(item => item.Lines.Length > 0)
            .Select(item => $"{Path.GetRelativePath(root, item.Path)}:{string.Join(",", item.Lines)}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "App.Services ist ein Service-Locator. Neue UI-Abhaengigkeiten per Konstruktor injizieren oder im Composition Root verdrahten:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void PlayerWindow_partials_do_not_import_ui_services_namespace()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");

        var offenders = Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
            .Where(path => File.ReadAllText(path).Contains("using AuswertungPro.Next.UI.Services;", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Partials sollen UI.Services nicht direkt importieren:\n" + string.Join("\n", offenders));
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).Replace('\\', '/');
}
