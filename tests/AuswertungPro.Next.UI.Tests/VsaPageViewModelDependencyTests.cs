using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaPageViewModelDependencyTests
{
    [Fact]
    public void ViewModel_speichert_weder_Shell_noch_ServiceProvider_als_Feld()
    {
        var fields = typeof(VsaPageViewModel).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ShellViewModel));
        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
        Assert.Contains(fields, field => field.FieldType == typeof(IStoredImportFilePathResolver));
    }

    [Fact]
    public void ViewModel_liest_gespeicherte_Importpfade_nicht_selbst()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "VsaPageViewModel.cs"));

        Assert.DoesNotContain("JsonSerializer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetFullPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.Combine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.IsPathRooted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Exists", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StoredImportFileRegistry.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadStoredXtfFiles", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadStoredPdfFiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lauf_nutzt_genau_die_uebergebenen_Dienste_und_aktualisiert_das_Projekt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"SewerStudio_VsaVm_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var xtfPath = Path.Combine(tempDir, "quelle.xtf");
            var pdfPath = Path.Combine(tempDir, "quelle.pdf");
            File.WriteAllText(xtfPath, "xtf");
            File.WriteAllText(pdfPath, "pdf");

            var project = new Project();
            project.Metadata["XTF_StoredFiles"] = "quelle.xtf";
            project.Metadata["PDF_StoredFiles"] = "quelle.pdf";

            var record = new HaltungRecord();
            record.SetFieldValue("Pruefungsresultat", "Sanierungsbedarf", FieldSource.Unknown, userEdited: false);
            record.SetFieldValue("VSA_Zustandsnote_D", "2", FieldSource.Unknown, userEdited: false);
            project.Data.Add(record);

            var xtf = new RecordingXtfImport();
            var pdf = new RecordingPdfImport();
            var storedImportPaths = new RecordingStoredImportFilePathResolver(
                [xtfPath],
                [pdfPath]);
            var vsa = new RecordingVsaEvaluation();
            var measures = new RecordingMeasureRecommendation();
            var statuses = new List<string>();
            var restoreLabels = new List<string>();
            var refreshCount = 0;

            var vm = new VsaPageViewModel(
                getProject: () => project,
                collectionLock: new object(),
                getProjectPath: () => Path.Combine(tempDir, "projekt.json"),
                getExplicitPdfToTextPath: () => "werkzeuge/pdftotext.exe",
                storedImportFilePaths: storedImportPaths,
                xtfImport: xtf,
                pdfImport: pdf,
                vsaEvaluation: vsa,
                measureRecommendation: measures,
                setStatus: statuses.Add,
                createImportRestorePoint: restoreLabels.Add,
                refreshTitleAndDirty: () => refreshCount++);

            await vm.RunCommand.ExecuteAsync(null);

            Assert.Single(xtf.Paths);
            Assert.Equal(xtfPath, xtf.Paths[0]);
            Assert.Equal(pdfPath, pdf.Path);
            Assert.Collection(
                storedImportPaths.Calls,
                call =>
                {
                    Assert.Same(project.Metadata, call.Metadata);
                    Assert.Equal("XTF_StoredFiles", call.MetadataKey);
                    Assert.Equal(Path.Combine(tempDir, "projekt.json"), call.ProjectFilePath);
                },
                call =>
                {
                    Assert.Same(project.Metadata, call.Metadata);
                    Assert.Equal("PDF_StoredFiles", call.MetadataKey);
                    Assert.Equal(Path.Combine(tempDir, "projekt.json"), call.ProjectFilePath);
                });
            Assert.Equal("werkzeuge/pdftotext.exe", pdf.PdfToTextPath);
            Assert.True(pdf.FillMissingOnly);
            Assert.Equal(1, vsa.CallCount);
            Assert.Equal(1, measures.CallCount);
            Assert.Equal(new[] { "VSA-Daten" }, restoreLabels);
            Assert.Equal(1, refreshCount);
            Assert.Equal("VSA berechnet + 1 Maßnahmen", statuses[^1]);
            Assert.Contains("Berechnet für 1 Records", vm.Summary, StringComparison.Ordinal);
            Assert.Equal("Kurzliner", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
            Assert.True(project.Dirty);
            Assert.False(vm.IsBusy);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Lauf_faehrt_ohne_aufgeloeste_Importdateien_mit_der_Bewertung_fort()
    {
        var project = new Project();
        project.Metadata["XTF_StoredFiles"] = "fehlt.xtf";
        project.Metadata["PDF_StoredFiles"] = "fehlt.pdf";
        var xtf = new RecordingXtfImport();
        var pdf = new RecordingPdfImport();
        var storedImportPaths = new RecordingStoredImportFilePathResolver([], []);
        var vsa = new RecordingVsaEvaluation();
        var restoreLabels = new List<string>();

        var vm = new VsaPageViewModel(
            getProject: () => project,
            collectionLock: new object(),
            getProjectPath: () => "C:/Projekt/Projektdateien/projekt.json",
            getExplicitPdfToTextPath: () => null,
            storedImportFilePaths: storedImportPaths,
            xtfImport: xtf,
            pdfImport: pdf,
            vsaEvaluation: vsa,
            measureRecommendation: new RecordingMeasureRecommendation(),
            setStatus: _ => { },
            createImportRestorePoint: restoreLabels.Add,
            refreshTitleAndDirty: () => { });

        await vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(0, xtf.CallCount);
        Assert.Equal(0, pdf.CallCount);
        Assert.Equal(1, vsa.CallCount);
        Assert.Equal(["VSA-Daten"], restoreLabels);
        Assert.Contains("Import-Quellen: XTF/M150/MDB=0", vm.Summary, StringComparison.Ordinal);
        Assert.Contains("Import-Quellen: PDF=0", vm.Summary, StringComparison.Ordinal);
    }

    private static ImportStats SuccessfulImport()
        => new(Found: 1, Created: 0, Updated: 1, Errors: 0, Uncertain: 0, Messages: Array.Empty<string>());

    private sealed class RecordingXtfImport : IXtfImportService
    {
        public IReadOnlyList<string> Paths { get; private set; } = Array.Empty<string>();
        public int CallCount { get; private set; }

        public Result<ImportStats> ImportXtfFiles(
            IEnumerable<string> xtfPaths,
            Project project,
            ImportRunContext? ctx = null)
        {
            CallCount++;
            Paths = xtfPaths.ToArray();
            return Result<ImportStats>.Success(SuccessfulImport());
        }
    }

    private sealed class RecordingPdfImport : IPdfImportService
    {
        public string? Path { get; private set; }
        public string? PdfToTextPath { get; private set; }
        public bool FillMissingOnly { get; private set; }
        public int CallCount { get; private set; }

        public Result<ImportStats> ImportPdf(
            string pdfPath,
            Project project,
            string? pdfToTextPath,
            bool fillMissingOnly = false,
            ImportRunContext? ctx = null)
        {
            CallCount++;
            Path = pdfPath;
            PdfToTextPath = pdfToTextPath;
            FillMissingOnly = fillMissingOnly;
            return Result<ImportStats>.Success(SuccessfulImport());
        }
    }

    private sealed class RecordingStoredImportFilePathResolver(
        IReadOnlyList<string> xtfPaths,
        IReadOnlyList<string> pdfPaths) : IStoredImportFilePathResolver
    {
        public List<(
            IDictionary<string, string> Metadata,
            string MetadataKey,
            string? ProjectFilePath)> Calls { get; } = [];

        public IReadOnlyList<string> ResolveExistingFiles(
            IDictionary<string, string> metadata,
            string metadataKey,
            string? projectFilePath)
        {
            Calls.Add((metadata, metadataKey, projectFilePath));
            return metadataKey switch
            {
                "XTF_StoredFiles" => xtfPaths,
                "PDF_StoredFiles" => pdfPaths,
                _ => []
            };
        }
    }

    private sealed class RecordingVsaEvaluation : IVsaEvaluationService
    {
        public int CallCount { get; private set; }

        public Result<IReadOnlyList<VsaConditionResult>> Evaluate(Project project)
        {
            CallCount++;
            return Result<IReadOnlyList<VsaConditionResult>>.Success(Array.Empty<VsaConditionResult>());
        }

        public Result<bool> EvaluateRecord(HaltungRecord record)
            => Result<bool>.Success(true);

        public Result<string> Explain(Project project, HaltungRecord record)
            => Result<string>.Success(string.Empty);
    }

    private sealed class RecordingMeasureRecommendation : IMeasureRecommendationService
    {
        public int CallCount { get; private set; }

        public MeasureRecommendationResult Recommend(HaltungRecord record, int maxSuggestions = 5)
        {
            CallCount++;
            return new MeasureRecommendationResult(
                Measures: new[] { "Kurzliner" },
                EstimatedTotalCost: 1200m,
                RenovierungInlinerM: null,
                RenovierungInlinerStk: null,
                AnschluesseVerpressen: null,
                ReparaturManschette: null,
                ReparaturKurzliner: 1,
                SimilarCasesCount: 3,
                UsedTrainedModel: false);
        }

        public MeasureLearningStats GetStats()
            => new(0, 0, 0, false, null, null, string.Empty);

        public MeasureModelTrainingResult TrainModel(int minSamples = 25)
            => new(false, 0, minSamples, string.Empty, null, null);

        public bool Learn(HaltungRecord record) => false;
    }
}
