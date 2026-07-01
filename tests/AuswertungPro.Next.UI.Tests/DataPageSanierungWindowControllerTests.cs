using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSanierungWindowControllerTests
{
    [Fact]
    public void Open_ignoriert_null_ohne_selected()
    {
        var dialogs = new CapturingDialogService();
        var shown = new List<DataPageSanierungWindowRequest>();
        var controller = CreateController(
            dialogs,
            getSelected: () => null,
            showWindow: shown.Add);

        controller.Open(null, InitialFocusMode.AiOptimization);

        Assert.Empty(shown);
        Assert.Null(dialogs.LastWarn);
    }

    [Fact]
    public void Open_warnt_bei_fehlendem_haltungsnamen()
    {
        var dialogs = new CapturingDialogService();
        var shown = new List<DataPageSanierungWindowRequest>();
        var controller = CreateController(dialogs, showWindow: shown.Add);

        controller.Open(Record(" "), InitialFocusMode.CostCalculator);

        Assert.Empty(shown);
        Assert.Equal(("Haltungsname fehlt in der Zeile.", "Sanierungsmassnahmen"), dialogs.LastWarn);
    }

    [Fact]
    public void Open_baut_request_ohne_ki_mit_empfohlenen_templates_und_cost_callback()
    {
        var dialogs = new CapturingDialogService();
        var record = Record(" H-01 ", "Inliner, Manschette");
        var applied = new List<(HaltungRecord Record, HoldingCost Cost)>();
        DataPageSanierungWindowRequest? request = null;
        var controller = CreateController(
            dialogs,
            parseRecommendedTemplates: raw =>
            {
                Assert.Equal("Inliner, Manschette", raw);
                return new[] { "A", "B" };
            },
            loadRuntimeSettings: () => Settings(enabled: false),
            applyCostsToRecord: (r, c) => applied.Add((r, c)),
            showWindow: r => request = r);

        controller.Open(record, InitialFocusMode.CostCalculator);

        Assert.NotNull(request);
        Assert.Same(record, request!.Record);
        Assert.Equal("H-01", request.Holding);
        Assert.Equal(InitialFocusMode.CostCalculator, request.Focus);
        Assert.Equal(new[] { "A", "B" }, request.RecommendedTemplates);
        Assert.Null(request.RuntimeSettings);
        Assert.Null(request.RuleRecommendation);

        var cost = new HoldingCost { Holding = "H-01" };
        request.ApplyCosts(cost);

        var appliedCost = Assert.Single(applied);
        Assert.Same(record, appliedCost.Record);
        Assert.Same(cost, appliedCost.Cost);
    }

    [Fact]
    public void Open_baut_rule_dto_wenn_ki_aktiv_und_regel_empfiehlt_massnahmen()
    {
        var dialogs = new CapturingDialogService();
        var record = Record();
        DataPageSanierungWindowRequest? request = null;
        var controller = CreateController(
            dialogs,
            loadRuntimeSettings: () => Settings(enabled: true),
            recommendMeasures: (r, maxSuggestions) =>
            {
                Assert.Same(record, r);
                Assert.Equal(5, maxSuggestions);
                return Recommendation("M1", "M2");
            },
            showWindow: r => request = r);

        controller.Open(record, InitialFocusMode.AiOptimization);

        Assert.NotNull(request);
        Assert.NotNull(request!.RuntimeSettings);
        Assert.Equal(new[] { "M1", "M2" }, request.RuleRecommendation?.Measures);
        Assert.Equal(123m, request.RuleRecommendation?.EstimatedCost);
        Assert.True(request.RuleRecommendation?.UsedTrainedModel);
    }

    [Fact]
    public void Transfer_callback_markiert_dirty_refresh_autosave_und_status()
    {
        var dialogs = new CapturingDialogService();
        var record = Record("H-01");
        var dirty = 0;
        var refreshed = new List<HaltungRecord>();
        var autosaves = 0;
        var statuses = new List<string>();
        DataPageSanierungWindowRequest? request = null;
        var controller = CreateController(
            dialogs,
            markProjectDirty: () => dirty++,
            refreshRecordInGrid: refreshed.Add,
            scheduleAutoSave: () => autosaves++,
            setStatus: statuses.Add,
            showWindow: r => request = r);

        controller.Open(record, InitialFocusMode.AiOptimization);
        request!.OnOptimizationTransferred();

        Assert.Equal(1, dirty);
        Assert.Same(record, Assert.Single(refreshed));
        Assert.Equal(1, autosaves);
        Assert.Equal("KI-Sanierungsvorschlag übertragen: H-01", Assert.Single(statuses));
    }

    private static DataPageSanierungWindowController CreateController(
        CapturingDialogService dialogs,
        Func<HaltungRecord?>? getSelected = null,
        Func<string?, IReadOnlyList<string>>? parseRecommendedTemplates = null,
        Func<AiRuntimeSettings>? loadRuntimeSettings = null,
        Func<HaltungRecord, int, MeasureRecommendationResult>? recommendMeasures = null,
        Action<HaltungRecord, HoldingCost>? applyCostsToRecord = null,
        Action? markProjectDirty = null,
        Action<HaltungRecord>? refreshRecordInGrid = null,
        Action? scheduleAutoSave = null,
        Action<string>? setStatus = null,
        Action<DataPageSanierungWindowRequest>? showWindow = null)
        => new(
            dialogs,
            getSelected ?? (() => null),
            parseRecommendedTemplates ?? (_ => Array.Empty<string>()),
            loadRuntimeSettings ?? (() => Settings(enabled: false)),
            recommendMeasures ?? ((_, _) => MeasureRecommendationResult.Empty),
            applyCostsToRecord ?? ((_, _) => { }),
            markProjectDirty ?? (() => { }),
            refreshRecordInGrid ?? (_ => { }),
            scheduleAutoSave ?? (() => { }),
            setStatus ?? (_ => { }),
            showWindow ?? (_ => { }));

    private static HaltungRecord Record(string? name = "H-01", string? recommended = null)
    {
        var record = new HaltungRecord();
        if (name is not null)
            record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        if (recommended is not null)
            record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", recommended, FieldSource.Manual, userEdited: false);
        return record;
    }

    private static AiRuntimeSettings Settings(bool enabled)
        => new(
            enabled,
            new Uri("http://localhost:11434"),
            "vision",
            "text",
            null,
            null,
            TimeSpan.FromSeconds(30),
            "5m",
            4096);

    private static MeasureRecommendationResult Recommendation(params string[] measures)
        => new(
            measures,
            123m,
            null,
            null,
            null,
            null,
            null,
            4,
            true);

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Message, string Title)? LastWarn { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => throw new NotSupportedException();

        public void Warn(string message, string title = "Warnung")
            => LastWarn = (message, title);

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
