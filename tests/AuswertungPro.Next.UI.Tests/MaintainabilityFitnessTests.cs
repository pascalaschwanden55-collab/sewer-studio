using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Tests;

public sealed class MaintainabilityFitnessTests
{
    private const int LargePartialTypeLimit = 2_000;

    private static readonly HashSet<string> ExistingLargeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    private const int FileRatchetThreshold = 900;
    private const int TypeRatchetThreshold = 1_500;

    /// <summary>
    /// Sperrklinke fuer Dateien ab 900 Zeilen (Stand 30.09.2026). Ein Eintrag darf nie wachsen; schrumpft die
    /// Datei, muss der Wert gesenkt werden, unter 900 Zeilen faellt der Eintrag weg. So ist jeder Fortschritt
    /// festgehalten und Code laeuft nicht bis knapp unter die harte Grenze von 1'000 Zeilen auf.
    /// </summary>
    private static readonly Dictionary<string, int> FrozenFileSizes = new(StringComparer.Ordinal)
    {
        ["src/AuswertungPro.Next.UI/ViewModels/TrainingStudioViewModel.cs"] = 1_000,
        ["src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml.cs"] = 1_000,
        ["src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs"] = 999,
        ["src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs"] = 997,
        ["src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs"] = 995,
        ["src/AuswertungPro.Next.Infrastructure/Reports/ProtocolPdfExporter.cs"] = 994,
        ["src/AuswertungPro.Next.UI/Views/Windows/StartupSplashWindow.Animation.cs"] = 985,
        ["src/AuswertungPro.Next.Application/Dossiers/Preview/DossierOutputPreviewTableCellMapper.cs"] = 984,
        ["src/AuswertungPro.Next.Infrastructure/HoldingFolderDistributor.PdfParsing.cs"] = 976,
        ["src/AuswertungPro.Next.Infrastructure/Import/MediaDistributionService.cs"] = 973,
        ["src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs"] = 973,
        ["src/AuswertungPro.Next.Infrastructure/Ai/Training/Services/PdfProtocolExtractor.cs"] = 969,
        ["src/AuswertungPro.Next.Infrastructure/Import/WinCan/WinCanDbImportService.cs"] = 958,
        ["src/AuswertungPro.Next.Infrastructure/Ai/OverlayToolService.cs"] = 954,
        ["src/AuswertungPro.Next.Infrastructure/Media/MediaConflictCenterService.cs"] = 954,
        ["src/AuswertungPro.Next.Infrastructure/Dossiers/DossierWordTemplateExportService.cs"] = 948,
        ["src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs"] = 948,
        ["src/AuswertungPro.Next.UI/ServiceProvider.cs"] = 939,
        ["src/AuswertungPro.Next.UI/Services/SystemMonitorService.cs"] = 937,
        ["src/AuswertungPro.Next.UI/ViewModels/Pages/DossiersPageViewModel.Actions.cs"] = 910,
        ["src/AuswertungPro.Next.Infrastructure/Ai/Training/ExportPlans/TrainingExportRegistryFileStore.cs"] = 909
    };

    /// <summary>
    /// Sperrklinke fuer Typen ab 1'500 Zeilen (alle Teildateien einer partial-Klasse zusammengezaehlt,
    /// Stand 30.09.2026). Gleiche Regeln wie bei <see cref="FrozenFileSizes"/>. Zusaetzlich gilt weiter die harte
    /// Grenze von 2'000 Zeilen fuer alle Typen ausser den hier eingetragenen.
    /// </summary>
    private static readonly Dictionary<string, int> ExistingLargePartialTypes = new(StringComparer.Ordinal)
    {
        ["AuswertungPro.Next.UI.Views.Windows.PlayerWindow"] = 4_246,
        ["AuswertungPro.Next.Infrastructure.HoldingFolderDistributor"] = 2_981,
        ["AuswertungPro.Next.UI.Views.Windows.DossierPreviewFieldPanel"] = 1_999,
        ["AuswertungPro.Next.UI.Views.Pages.DataPage"] = 1_945,
        ["AuswertungPro.Next.UI.ViewModels.Pages.BuilderPageViewModel"] = 1_964,
        ["AuswertungPro.Next.UI.ViewModels.TrainingStudioViewModel"] = 1_951,
        ["AuswertungPro.Next.UI.ViewModels.Pages.SchaechtePageViewModel"] = 1_921,
        ["AuswertungPro.Next.UI.ViewModels.Pages.ExportPageViewModel"] = 1_889,
        ["AuswertungPro.Next.Infrastructure.Import.WinCan.WinCanDbImportService"] = 1_821,
        ["AuswertungPro.Next.UI.ViewModels.ShellViewModel"] = 1_783,
        ["AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans.TrainingExportRegistryFileStore"] = 1_745,
        ["AuswertungPro.Next.UI.Views.Pages.SchaechtePage"] = 1_687,
        ["AuswertungPro.Next.UI.ViewModels.Pages.DossiersPageViewModel"] = 1_642,
        ["AuswertungPro.Next.UI.Views.Windows.StartupSplashWindow"] = 1_618,
        ["AuswertungPro.Next.UI.ViewModels.Pages.DataPageViewModel"] = 1_564,
        ["AuswertungPro.Next.UI.ServiceProvider"] = 1_557,
        ["AuswertungPro.Next.UI.Views.Windows.PhotoMeasurementWindow"] = 1_556
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
        var root = TestRepoPaths.FindRepositoryRoot();
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
        var root = TestRepoPaths.FindRepositoryRoot();
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
            "Veraltete Einträge aus der Grossdatei-Ausnahmeliste entfernen:\n"
            + string.Join("\n", staleEntries));
    }

    [Fact]
    public void Frozen_files_cannot_grow()
    {
        var current = FindFileSizes();
        var offenders = FrozenFileSizes
            .Where(entry => current.TryGetValue(entry.Key, out var lines) && lines > entry.Value)
            .Select(entry => $"{entry.Key}: alt {entry.Value}, neu {current[entry.Key]} Zeilen")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Diese Dateien sind eingefroren und dürfen nicht wachsen. Verantwortung in eine eigene Klasse "
            + "auslagern, nicht in eine neue Teildatei verschieben:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Frozen_file_values_follow_shrinking_files()
    {
        var current = FindFileSizes();
        var stale = new List<string>();
        foreach (var (path, frozen) in FrozenFileSizes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!current.TryGetValue(path, out var lines))
                stale.Add($"{path}: Datei gibt es nicht mehr, Eintrag entfernen");
            else if (lines <= FileRatchetThreshold)
                stale.Add($"{path}: nur noch {lines} Zeilen (Schwelle {FileRatchetThreshold}), Eintrag entfernen");
            else if (lines < frozen)
                stale.Add($"{path}: Wert auf {lines} senken (bisher {frozen})");
        }

        Assert.True(
            stale.Count == 0,
            "Die Dateien sind kleiner geworden. Werte in FrozenFileSizes nachziehen, damit der Fortschritt "
            + "festgehalten bleibt:\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void Partial_types_cannot_hide_growth_across_many_small_files()
    {
        var offenders = FindPartialTypeSizes()
            .Where(type => type.Lines > LargePartialTypeLimit
                || ExistingLargePartialTypes.ContainsKey(type.Name))
            .Where(type => !ExistingLargePartialTypes.TryGetValue(type.Name, out var baseline)
                || type.Lines > baseline)
            .OrderByDescending(type => type.Lines)
            .Select(type => ExistingLargePartialTypes.TryGetValue(type.Name, out var baseline)
                ? $"{type.Name}: alt {baseline}, neu {type.Lines} Zeilen in {type.FileCount} Dateien"
                : $"{type.Name} ({type.Lines} Zeilen in {type.FileCount} Dateien)")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Neue God-Classes oder Wachstum eingefrorener Klassen sind nicht erlaubt. "
            + "Verantwortung zuerst in Controller oder Services auslagern, nicht in eine neue Teildatei "
            + "verschieben:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Partial_type_baseline_follows_shrinking_types()
    {
        var current = FindPartialTypeSizes()
            .ToDictionary(type => type.Name, type => type.Lines, StringComparer.Ordinal);
        var stale = new List<string>();
        foreach (var (name, frozen) in ExistingLargePartialTypes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!current.TryGetValue(name, out var lines))
                stale.Add($"{name}: Typ gibt es nicht mehr, Eintrag entfernen");
            else if (lines <= TypeRatchetThreshold)
                stale.Add($"{name}: nur noch {lines} Zeilen (Schwelle {TypeRatchetThreshold}), Eintrag entfernen");
            else if (lines < frozen)
                stale.Add($"{name}: Wert auf {lines} senken (bisher {frozen})");
        }

        Assert.True(
            stale.Count == 0,
            "Die Klassen sind kleiner geworden. Werte in ExistingLargePartialTypes nachziehen, damit der "
            + "Fortschritt festgehalten bleibt:\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void Static_di_bypass_facades_are_frozen_to_documented_whitelist()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
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
        var sourceRoot = Path.Combine(TestRepoPaths.FindRepositoryRoot(), "src");
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
            "Neue veraenderbare Current/Use-Fassade gefunden. Neue Dienste müssen per Konstruktor " +
            "injiziert werden:\n  " + string.Join("\n  ", newFacades));
        Assert.True(
            removedFacades.Length == 0,
            "Diese Current/Use-Altstellen wurden entfernt. Bitte aus der Altliste löschen, damit " +
            "die Obergrenze dauerhaft sinkt:\n  " + string.Join("\n  ", removedFacades));
    }

    private static IReadOnlyDictionary<string, int> FindFileSizes()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
        var sourceRoot = Path.Combine(root, "src");
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => File.ReadLines(path).Count(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<PartialTypeSize> FindPartialTypeSizes()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
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
