using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MaintainabilityFitnessTests
{
    private const int LargePartialTypeLimit = 2_000;

    private static readonly HashSet<string> ExistingLargeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    private static readonly Dictionary<string, int> ExistingLargePartialTypes = new(StringComparer.Ordinal)
    {
        ["AuswertungPro.Next.UI.Views.Windows.PlayerWindow"] = 4_263,
        ["AuswertungPro.Next.Infrastructure.HoldingFolderDistributor"] = 3_064
    };

    private static readonly HashSet<string> ExistingMutableServiceFacades = new(StringComparer.Ordinal)
    {
    };

    private static readonly HashSet<string> ImmutableCompatibilityFacades = new(StringComparer.Ordinal)
    {
        "AuswertungPro.Next.Infrastructure/Ai/Configuration/AiSettingsFactory.cs",
        "AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseHealthChecker.cs",
        "AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBasePaths.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Ollama/GpuModelSelector.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Pipeline/PipelineEnvironmentOptions.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Pipeline/PipelineTraceWriter.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Pipeline/SidecarTokenResolver.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Pipeline/SidecarTelemetryWriter.cs",
        "AuswertungPro.Next.Infrastructure/Ai/ProcessOutputReader.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Shared/FfmpegLocator.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Training/FrameStore.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Training/SelfTrainingHistoryStore.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Training/TrainingCenterSettingsStore.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Training/TrainingSamplesStore.cs",
        "AuswertungPro.Next.Infrastructure/Ai/VideoFrameExtractor.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Teacher/TeacherAnnotationStore.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Teacher/VsaYoloClassMap.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Sanierung/AiOptimizationSessionStore.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Startup/AiStartedProcessLifetime.cs",
        "AuswertungPro.Next.Infrastructure/Ai/Startup/SidecarScriptLocator.cs",
        "AuswertungPro.Next.Infrastructure/Backup/BackupManifestIntegrity.cs",
        "AuswertungPro.Next.Infrastructure/Backup/RepoRootLocator.cs",
        "AuswertungPro.Next.Infrastructure/Costs/NpkLeistungsverzeichnisExcelExporter.cs",
        "AuswertungPro.Next.Infrastructure/HoldingDistribution/DistributionFileTransfer.cs",
        "AuswertungPro.Next.Infrastructure/HoldingDistribution/DistributionPdfPageReader.cs",
        "AuswertungPro.Next.Infrastructure/HoldingDistribution/PdfTextLayerRewriter.cs",
        "AuswertungPro.Next.Infrastructure/HoldingDistribution/ShaftPdfSelectionExpander.cs",
        "AuswertungPro.Next.Infrastructure/HoldingDistribution/VideoConflictArtifacts.cs",
        "AuswertungPro.Next.Infrastructure/Import/Ibak/IbakFdbConnectionOptions.cs",
        "AuswertungPro.Next.Infrastructure/Import/Ibak/KiasExportPattern.cs",
        "AuswertungPro.Next.Infrastructure/Import/Kins/KinsDbfWhitelistEnricher.cs",
        "AuswertungPro.Next.Infrastructure/Import/Kins/KinsDvdTextEnricher.cs",
        "AuswertungPro.Next.Infrastructure/Import/Kins/KinsGesamtprotokollLocator.cs",
        "AuswertungPro.Next.Infrastructure/Import/Pdf/AtomicPdfFileReplacer.cs",
        "AuswertungPro.Next.Infrastructure/Import/Pdf/PdfImportSafetyPolicy.cs",
        "AuswertungPro.Next.Infrastructure/Import/Pdf/PdfFormFieldExtractor.cs",
        "AuswertungPro.Next.Infrastructure/Import/Pdf/PdfOcrExtractor.cs",
        "AuswertungPro.Next.Infrastructure/Import/Pdf/PdfTextExtractor.cs",
        "AuswertungPro.Next.Infrastructure/Import/Xtf/M150MdbRowReader.cs",
        "AuswertungPro.Next.Infrastructure/Import/Xtf/M150SourceFileReader.cs",
        "AuswertungPro.Next.Infrastructure/Map/HaltungCadastreExtractor.cs",
        "AuswertungPro.Next.Infrastructure/Map/HaltungCadastreIndex.cs",
        "AuswertungPro.Next.Infrastructure/Media/PdfMergeHelper.cs",
        "AuswertungPro.Next.Infrastructure/Telemetry/TelemetryPathResolver.cs",
        "AuswertungPro.Next.Infrastructure/Vsa/VsaShadowTelemetryWriter.cs",
        "AuswertungPro.Next.UI/Services/FullBackupSourcesFactory.cs",
        "AuswertungPro.Next.UI/Services/CodeUsageTrackers.cs",
        "AuswertungPro.Next.UI/Services/DialogHost.cs",
        "AuswertungPro.Next.UI/Services/SafeShellOpen.cs",
        "AuswertungPro.Next.UI/Theme/StatusColors.cs"
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
    public void Partial_types_cannot_hide_growth_across_many_small_files()
    {
        var offenders = FindPartialTypeSizes()
            .Where(type => type.Lines > LargePartialTypeLimit)
            .Where(type => !ExistingLargePartialTypes.TryGetValue(type.Name, out var baseline)
                || type.Lines > baseline)
            .OrderByDescending(type => type.Lines)
            .Select(type => $"{type.Name} ({type.Lines} Zeilen in {type.FileCount} Dateien)")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Neue God-Classes oder Wachstum bestehender God-Classes sind nicht erlaubt. "
            + "Verantwortung zuerst in Controller oder Services auslagern:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Partial_type_baseline_contains_only_types_that_are_still_large()
    {
        var current = FindPartialTypeSizes()
            .ToDictionary(type => type.Name, type => type.Lines, StringComparer.Ordinal);
        var staleEntries = ExistingLargePartialTypes
            .Where(entry => !current.TryGetValue(entry.Key, out var lines)
                || lines <= LargePartialTypeLimit)
            .Select(entry => entry.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            staleEntries.Length == 0,
            "Diese Klassen sind nicht mehr zu gross und muessen aus der Ausnahme entfernt werden:\n"
            + string.Join("\n", staleEntries));
    }

    [Fact]
    public void Static_di_bypass_facades_are_frozen_to_documented_whitelist()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
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

    [Fact]
    public void Mutable_service_facades_can_only_shrink()
    {
        var sourceRoot = Path.Combine(TestRepoPaths.FindRepoRoot(), "src");
        var useMethod = new Regex(
            @"\b(?:public|internal)\s+static\s+void\s+Use(?:Provider|Service)?\s*\(",
            RegexOptions.CultureInvariant);
        var settableCurrent = new Regex(
            @"\bpublic\s+static\s+[^{;]+?\s+Current\s*\{[^}]*\bset\s*;",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);

        var current = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                var relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
                return useMethod.IsMatch(source) && !ImmutableCompatibilityFacades.Contains(relative)
                    || settableCurrent.IsMatch(source)
                    || relative == "AuswertungPro.Next.UI/Services/DialogHost.cs"
                        && source.Contains("public static void Configure(", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        var newFacades = current.Except(ExistingMutableServiceFacades).OrderBy(path => path).ToArray();
        var removedFacades = ExistingMutableServiceFacades.Except(current).OrderBy(path => path).ToArray();

        Assert.True(
            newFacades.Length == 0,
            "Neue veraenderbare Current/Use-Fassade gefunden. Neue Dienste muessen per Konstruktor " +
            "injiziert werden:\n  " + string.Join("\n  ", newFacades));
        Assert.True(
            removedFacades.Length == 0,
            "Diese Current/Use-Altstellen wurden entfernt. Bitte aus der Altliste loeschen, damit " +
            "die Obergrenze dauerhaft sinkt:\n  " + string.Join("\n  ", removedFacades));
    }

    private static IReadOnlyList<PartialTypeSize> FindPartialTypeSizes()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var sourceRoot = Path.Combine(root, "src");
        var separator = Path.DirectorySeparatorChar;
        var namespaceRegex = new Regex(
            @"(?m)^\s*namespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]",
            RegexOptions.CultureInvariant);
        var partialTypeRegex = new Regex(
            @"(?m)^\s*(?:(?:public|internal|protected|private|sealed|abstract|static|readonly|ref|unsafe|new)\s+)*partial\s+(?:class|struct|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant);

        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                var namespaceName = namespaceRegex.Match(source).Groups["name"].Value;
                var lineCount = File.ReadLines(path).Count();
                return partialTypeRegex.Matches(source)
                    .Select(match => string.IsNullOrWhiteSpace(namespaceName)
                        ? match.Groups["name"].Value
                        : $"{namespaceName}.{match.Groups["name"].Value}")
                    .Distinct(StringComparer.Ordinal)
                    .Select(name => new PartialTypeFile(name, lineCount));
            })
            .GroupBy(type => type.Name, StringComparer.Ordinal)
            .Select(group => new PartialTypeSize(
                group.Key,
                group.Sum(type => type.Lines),
                group.Count()))
            .ToArray();
    }

    private sealed record PartialTypeFile(string Name, int Lines);

    private sealed record PartialTypeSize(string Name, int Lines, int FileCount);
}
