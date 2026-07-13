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

    private static ImportStats SuccessfulImport()
        => new(Found: 1, Created: 0, Updated: 1, Errors: 0, Uncertain: 0, Messages: Array.Empty<string>());

    private sealed class RecordingXtfImport : IXtfImportService
    {
        public IReadOnlyList<string> Paths { get; private set; } = Array.Empty<string>();

        public Result<ImportStats> ImportXtfFiles(
            IEnumerable<string> xtfPaths,
            Project project,
            ImportRunContext? ctx = null)
        {
            Paths = xtfPaths.ToArray();
            return Result<ImportStats>.Success(SuccessfulImport());
        }
    }

    private sealed class RecordingPdfImport : IPdfImportService
    {
        public string? Path { get; private set; }
        public string? PdfToTextPath { get; private set; }
        public bool FillMissingOnly { get; private set; }

        public Result<ImportStats> ImportPdf(
            string pdfPath,
            Project project,
            string? pdfToTextPath,
            bool fillMissingOnly = false,
            ImportRunContext? ctx = null)
        {
            Path = pdfPath;
            PdfToTextPath = pdfToTextPath;
            FillMissingOnly = fillMissingOnly;
            return Result<ImportStats>.Success(SuccessfulImport());
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
