using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MaintainabilityFitnessTests
{
    private static readonly HashSet<string> ExistingLargeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.PdfParsing.cs",
        "src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs",
        "src/AuswertungPro.Next.UI/Views/Windows/PhotoMeasurementWindow.xaml.cs",
        "src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs",
        "src/AuswertungPro.Next.UI/Services/SystemMonitorService.cs",
        "src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml.cs",
        "src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.cs",
        "src/AuswertungPro.Next.UI/ViewModels/Windows/CostCalculatorViewModel.cs",
        "src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs",
        "src/AuswertungPro.Next.UI/Views/Windows/StartupSplashWindow.xaml.cs",
    };

    [Fact]
    public void No_new_production_file_exceeds_1000_lines()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var sourceRoot = Path.Combine(root, "src");
        var offenders = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                Relative = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Lines = File.ReadLines(path).Count()
            })
            .Where(file => file.Lines > 1000 && !ExistingLargeFiles.Contains(file.Relative))
            .OrderByDescending(file => file.Lines)
            .Select(file => $"{file.Relative} ({file.Lines} Zeilen)")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Neue Grossdateien sind nicht erlaubt. Verantwortung zuerst in kleinere Klassen teilen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Large_file_whitelist_contains_only_files_that_are_still_large()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var staleEntries = ExistingLargeFiles
            .Where(relativePath =>
            {
                var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(path) || File.ReadLines(path).Count() <= 1000;
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            staleEntries.Length == 0,
            "Veraltete Eintraege aus der Grossdatei-Ausnahmeliste entfernen:\n"
            + string.Join("\n", staleEntries));
    }

    [Fact]
    public void Static_di_bypass_facades_are_frozen_to_documented_whitelist()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["public static IStatusColorService Current"] = "src/AuswertungPro.Next.UI/Theme/StatusColors.cs",
            ["public static ICodeUsageTracker Current"] = "src/AuswertungPro.Next.UI/Services/CodeUsageTrackers.cs",
            ["public static IDialogService Current"] = "src/AuswertungPro.Next.UI/Services/DialogHost.cs",
            ["public static ICodeCatalogProvider? CurrentCatalog"] = "src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs"
        };

        foreach (var (marker, expectedFile) in expected)
        {
            var matches = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => File.ReadAllText(path).Contains(marker, StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .ToArray();

            Assert.Equal(new[] { expectedFile }, matches);
        }
    }
}
