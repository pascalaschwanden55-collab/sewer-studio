using System.Collections.ObjectModel;
using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageMeasureSuggestionControllerTests
{
    // Der Betrag wird schweizerisch dargestellt, egal was Windows eingestellt hat.
    // Ohne diese Festlegung zeigte derselbe Stand auf dem Entwicklerrechner 1'250.00 und
    // auf dem englischen CI-Rechner 1,250.00 — der Test wurde dort rot, ohne dass sich am
    // Code etwas geaendert hatte.
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("de-CH")]
    public void Der_Kostenbetrag_haengt_nicht_von_der_Rechnerkultur_ab(string kultur)
    {
        var vorher = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(kultur);
        try
        {
            var record = Record("H1");
            var dialogs = new CapturingDialogService();
            var controller = CreateController(
                dialogs,
                new FakeMeasureRecommendationService(
                    _ => Recommendation(new[] { "Inliner" }, 1250m, similarCases: 3, trained: true)),
                selected: null,
                recommendedOptions: new ObservableCollection<string>());

            controller.Suggest(record);

            var erwartet = 1250m.ToString("N2", CultureInfo.GetCultureInfo("de-CH"));
            Assert.Contains($"Geschaetzte Kosten: {erwartet}", dialogs.LastInfo!.Value.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = vorher;
        }
    }

    [Fact]
    public void Suggest_nutzt_selected_fallback_und_meldet_fehlende_vorschlaege()
    {
        var record = Record("H1");
        var dialogs = new CapturingDialogService();
        var service = new FakeMeasureRecommendationService(_ => MeasureRecommendationResult.Empty);
        var dirty = 0;
        var controller = CreateController(
            dialogs,
            service,
            selected: record,
            markDirty: () => dirty++);

        controller.Suggest(null);

        Assert.Equal(("Noch keine Vorschlaege verfuegbar. Bitte zuerst einige Haltungen mit Massnahmen bewerten.", "Massnahmen"), dialogs.LastInfo);
        var requested = Assert.Single(service.RequestedRecords);
        Assert.Same(record, requested);
        Assert.Equal(0, dirty);
        Assert.True(string.IsNullOrEmpty(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen")));
    }

    [Fact]
    public void Suggest_wendet_empfehlung_an_und_aktualisiert_status_dialog_und_dropdown()
    {
        var record = Record("H1");
        var dialogs = new CapturingDialogService();
        var options = new ObservableCollection<string>();
        var statuses = new List<string>();
        var learning = new List<(int? SimilarCases, decimal? EstimatedCost)>();
        var dirty = 0;
        var recommendation = Recommendation(new[] { "Inliner", "Manschette" }, 1250m, similarCases: 3, trained: true);
        var controller = CreateController(
            dialogs,
            new FakeMeasureRecommendationService(_ => recommendation),
            selected: null,
            recommendedOptions: options,
            markDirty: () => dirty++,
            setStatus: statuses.Add,
            updateLearningInfo: (similar, cost) => learning.Add((similar, cost)));

        controller.Suggest(record);

        Assert.Equal("Inliner" + Environment.NewLine + "Manschette", record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        Assert.Equal("1250.00", record.GetFieldValue("Kosten"));
        Assert.Equal(new[] { "Inliner", "Manschette" }, options);
        Assert.Equal(1, dirty);
        Assert.Equal("Maßnahmenvorschlag mit Kostenschätzung gesetzt (1250.00, KI-Modell)", statuses.Single());
        Assert.Equal((3, 1250m), learning.Single());
        Assert.Equal(
            ("Inliner\nManschette\n\nGeschaetzte Kosten: 1’250.00\n\nQuelle: KI-Modell (3 aehnliche Faelle)", "Empfohlene Sanierungsmassnahmen"),
            dialogs.LastInfo);
    }

    private static DataPageMeasureSuggestionController CreateController(
        CapturingDialogService dialogs,
        IMeasureRecommendationService service,
        HaltungRecord? selected = null,
        ObservableCollection<string>? recommendedOptions = null,
        Action? markDirty = null,
        Action<string>? setStatus = null,
        Action<int?, decimal?>? updateLearningInfo = null)
        => new(
            dialogs,
            service,
            getSelected: () => selected,
            addRecommendedOption: value => AddIfMissing(recommendedOptions ?? new ObservableCollection<string>(), value),
            markProjectDirty: markDirty ?? (() => { }),
            setStatus: setStatus ?? (_ => { }),
            updateLearningInfo: updateLearningInfo ?? ((_, _) => { }));

    private static void AddIfMissing(ObservableCollection<string> options, string value)
    {
        if (!options.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
            options.Add(value);
    }

    private static HaltungRecord Record(
        string holding,
        string? pruefung = null,
        string? existingMeasures = null,
        bool userEditedMeasures = false)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holding, FieldSource.Manual, userEdited: false);
        if (pruefung is not null)
            record.SetFieldValue("Pruefungsresultat", pruefung, FieldSource.Manual, userEdited: false);
        if (existingMeasures is not null)
            record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", existingMeasures, FieldSource.Manual, userEdited: userEditedMeasures);
        return record;
    }

    private static MeasureRecommendationResult Recommendation(
        IReadOnlyList<string> measures,
        decimal? estimatedCost = null,
        int? similarCases = null,
        bool trained = false)
        => new(
            measures,
            estimatedCost,
            RenovierungInlinerM: null,
            RenovierungInlinerStk: null,
            AnschluesseVerpressen: null,
            ReparaturManschette: null,
            ReparaturKurzliner: null,
            similarCases,
            trained);

    private sealed class FakeMeasureRecommendationService : IMeasureRecommendationService
    {
        private readonly Func<HaltungRecord, MeasureRecommendationResult> _recommend;

        public FakeMeasureRecommendationService(Func<HaltungRecord, MeasureRecommendationResult> recommend)
        {
            _recommend = recommend;
        }

        public List<HaltungRecord> RequestedRecords { get; } = new();

        public MeasureRecommendationResult Recommend(HaltungRecord record, int maxSuggestions = 5)
        {
            Assert.Equal(5, maxSuggestions);
            RequestedRecords.Add(record);
            return _recommend(record);
        }

        public MeasureLearningStats GetStats()
            => throw new NotSupportedException();

        public MeasureModelTrainingResult TrainModel(int minSamples = 25)
            => throw new NotSupportedException();

        public bool Learn(HaltungRecord record)
            => throw new NotSupportedException();
    }

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Message, string Title)? LastInfo { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => LastInfo = (message, title);

        public void Warn(string message, string title = "Warnung")
            => throw new NotSupportedException();

        public void Error(string message, string title = "Fehler")
            => throw new NotSupportedException();

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
