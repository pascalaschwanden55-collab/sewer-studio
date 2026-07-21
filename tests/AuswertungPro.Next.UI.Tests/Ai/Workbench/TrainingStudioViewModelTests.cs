using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Verhaltenstests fuer das Pruefplatz-ViewModel (Etappe 1, Aufgabe 4).
/// Tastatur-Arbeitsfluss: Box ziehen → Vorschlag/Maske → Akzeptieren/Korrigieren → naechstes.
/// </summary>
public sealed class TrainingStudioViewModelTests
{
    private static readonly BoundingBox TestBox = new(0.5, 0.5, 0.2, 0.2);

    private static WorkbenchItem Foto(string frame = @"C:\frames\f.jpg")
        => new(frame, "case1", 1.0, 1.0, HaltungName: null, VideoPath: null, PipeDiameterMm: 300);

    private static TrainingStudioViewModel CreateVm(
        FakeWorkbench wb,
        IReadOnlyList<WorkbenchItem>? items = null,
        Func<string, string?>? labelLookup = null,
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? ensureAiReady = null)
        => new(
            wb,
            () => items ?? new[] { Foto() },
            "Pascal",
            labelLookup ?? (c => c),
            ensureAiReady);

    [Fact]
    public async Task BoxDrawn_fuellt_Maske_Vorschlag_und_SelectedCode_auf_Top()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.NotNull(vm.Segmentation);
        Assert.NotNull(vm.Suggestion);
        Assert.Equal("BAB", vm.SelectedCode);
        Assert.Equal((BoundingBox?)TestBox, vm.CurrentBox);
        Assert.False(string.IsNullOrWhiteSpace(vm.Beschreibung));
    }

    [Fact]
    public async Task Accept_speichert_ohne_Korrektur_und_geht_zum_naechsten_Item()
    {
        var wb = new FakeWorkbench();
        var items = new[] { Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg") };
        var vm = CreateVm(wb, items);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        var decision = Assert.Single(wb.SavedDecisions);
        Assert.False(decision.WasCorrected);
        Assert.Equal("BAB", decision.VsaCode);
        Assert.Equal("Pascal", decision.ConfirmedByUser);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(@"C:\b.jpg", vm.CurrentImagePath);
    }

    [Fact]
    public async Task Correct_mit_geaendertem_Code_markiert_WasCorrected()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);   // SelectedCode = Top ("BAB")

        vm.SelectedCode = "BBA";                          // Mensch weicht ab
        await vm.CorrectCommand.ExecuteAsync(null);

        var decision = Assert.Single(wb.SavedDecisions);
        Assert.True(decision.WasCorrected);
        Assert.Equal("BBA", decision.VsaCode);
    }

    [Fact]
    public async Task Manuell_geaenderter_Code_wird_auch_mit_Akzeptieren_als_Korrektur_zurueckgegeben()
    {
        var wb = new FakeWorkbench();
        var items = new[] { Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg") };
        var vm = CreateVm(wb, items);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        vm.SelectedCode = " bba ";
        await vm.AcceptCommand.ExecuteAsync(null);

        var decision = Assert.Single(wb.SavedDecisions);
        Assert.True(decision.WasCorrected);
        Assert.Equal("BBA", decision.VsaCode);
        Assert.Contains("Lerndaten", vm.StatusText);
        Assert.Equal(1, vm.CurrentIndex);
    }

    [Fact]
    public async Task Abweisung_zeigt_Meldung_und_bleibt_beim_Item()
    {
        var wb = new FakeWorkbench
        {
            SaveResult = new WorkbenchSaveResult(false, "Eval-Schutz: Bild gehoert zum Mess-Set.", null, "-", null),
        };
        var items = new[] { Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg") };
        var vm = CreateVm(wb, items);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Contains("Eval", vm.StatusText);
        Assert.Equal(0, vm.CurrentIndex);        // bleibt beim selben Item
        Assert.Single(wb.SavedDecisions);        // SaveAsync lief, aber ohne Weiterspringen
        Assert.Equal(@"C:\a.jpg", vm.CurrentImagePath);
    }

    [Fact]
    public async Task BoxDrawn_zweiter_Lauf_bricht_das_CTS_des_ersten_ab()
    {
        var wb = new FakeWorkbench
        {
            SegmentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        var first = vm.BoxDrawnCommand.ExecuteAsync(TestBox);   // haengt am Gate
        Assert.Single(wb.SegmentTokens);

        var second = vm.BoxDrawnCommand.ExecuteAsync(TestBox);  // eigenes CTS, bricht den ersten ab
        Assert.Equal(2, wb.SegmentTokens.Count);
        Assert.True(wb.SegmentTokens[0].IsCancellationRequested);
        Assert.False(wb.SegmentTokens[1].IsCancellationRequested);

        wb.SegmentGate.SetResult();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Neue_Box_entfernt_die_alte_Maske_sofort()
    {
        var wb = new FakeWorkbench
        {
            SegmentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        vm.Segmentation = wb.SegResult;
        vm.Suggestion = wb.SugResult;

        var drawing = vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.Null(vm.Segmentation);
        Assert.Null(vm.Suggestion);

        wb.SegmentGate.SetResult();
        await drawing;
    }

    [Fact]
    public void LoadItems_setzt_Liste_und_zeigt_erstes_Bild()
    {
        var vm = CreateVm(new FakeWorkbench());

        vm.LoadItems(new[] { Foto(@"C:\x.jpg"), Foto(@"C:\y.jpg") });

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\x.jpg", vm.CurrentImagePath);
    }

    [Fact]
    public void SetSeverity_uebernimmt_gueltige_Stufe_und_ignoriert_Unfug()
    {
        var vm = CreateVm(new FakeWorkbench());

        vm.SetSeverityCommand.Execute("3");
        Assert.Equal(3, vm.Severity);

        vm.SetSeverityCommand.Execute("9");     // ausserhalb 1..5
        Assert.Equal(3, vm.Severity);

        vm.SetSeverityCommand.Execute("abc");   // kein int
        Assert.Equal(3, vm.Severity);
    }

    [Fact]
    public void SelectCode_uebernimmt_Kandidatencode()
    {
        var vm = CreateVm(new FakeWorkbench());

        vm.SelectCodeCommand.Execute("BAB");

        Assert.Equal("BAB", vm.SelectedCode);
    }

    [Fact]
    public async Task Vorschlaege_und_Codefeld_zeigen_den_Katalog_Klartext()
    {
        var vm = CreateVm(
            new FakeWorkbench(),
            labelLookup: code => code switch
            {
                "BAB" => "Riss",
                "BBA" => "Wurzeln/Bewuchs",
                _ => null,
            });
        vm.LoadQueueCommand.Execute(null);

        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        var candidate = Assert.Single(vm.SuggestionCandidates);
        Assert.Equal("BAB", candidate.VsaCode);
        Assert.Equal("Riss", candidate.Klartext);
        Assert.Equal("Riss", vm.SelectedCodeLabel);

        vm.SelectedCode = "bba";
        Assert.Equal("Wurzeln/Bewuchs", vm.SelectedCodeLabel);
        Assert.StartsWith("Wurzeln/Bewuchs", vm.Beschreibung);

        vm.Beschreibung = "Vom Menschen genauer beschriebener Wurzeleinwuchs";
        vm.SelectedCode = "BAB";
        Assert.Equal("Vom Menschen genauer beschriebener Wurzeleinwuchs", vm.Beschreibung);
    }

    [Fact]
    public async Task BoxDrawn_mit_unbrauchbarem_Frame_zeigt_QualityWarning()
    {
        var wb = new FakeWorkbench
        {
            SugResult = new WorkbenchSuggestion(
                Array.Empty<WorkbenchCodeCandidate>(), FrameUsable: false, "zu dunkel", IsBend: false),
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.True(vm.ShowQualityWarning);
        Assert.Contains("dunkel", vm.QualityWarning);
    }

    [Fact]
    public async Task BoxDrawn_bei_Sidecar_Fehler_zeigt_Meldung_statt_Absturz()
    {
        var wb = new FakeWorkbench { SuggestThrows = new InvalidOperationException("Sidecar nicht erreichbar") };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        // Darf NICHT werfen (sonst globaler App-Crash-Dialog).
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.NotNull(vm.Segmentation);
        Assert.StartsWith("Maske ist sichtbar.", vm.StatusText);
        Assert.Contains("Programmlog", vm.StatusText);
        Assert.DoesNotContain("Sidecar nicht erreichbar", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task BoxDrawn_bei_nicht_erreichbarem_Sidecar_erklaert_den_KI_Start()
    {
        var wb = new FakeWorkbench
        {
            SuggestThrows = new SidecarUnavailableException("localhost:8100 nicht erreichbar"),
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.NotNull(vm.Segmentation);
        Assert.Contains("KI starten", vm.StatusText);
        Assert.DoesNotContain("localhost:8100", vm.StatusText);
    }

    [Fact]
    public async Task StartAi_verwendet_den_Startworkflow_und_zeigt_das_Ergebnis()
    {
        var called = false;
        var vm = CreateVm(
            new FakeWorkbench(),
            ensureAiReady: (progress, _) =>
            {
                called = true;
                progress.Report("Vision-KI startet...");
                return Task.FromResult((true, "Vision-KI bereit. Foto laden und Box ziehen."));
            });

        await vm.StartAiCommand.ExecuteAsync(null);

        Assert.True(called);
        Assert.Equal("Vision-KI bereit. Foto laden und Box ziehen.", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Accept_bei_Speicher_Fehler_zeigt_Meldung_statt_Absturz()
    {
        var wb = new FakeWorkbench { SaveThrows = new InvalidOperationException("Speichern fehlgeschlagen") };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        // Darf NICHT werfen.
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Contains("Programmlog", vm.StatusText);
        Assert.DoesNotContain("Speichern fehlgeschlagen", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void ApplyCodeSelection_uebernimmt_nichtleere_Werte_und_behaelt_Bestehendes()
    {
        var vm = CreateVm(new FakeWorkbench());
        vm.Severity = 2;   // am Pruefplatz gesetzte Stufe

        vm.ApplyCodeSelection("BABBC", 3.0, severity: null);   // Codierfenster liefert keine Stufe

        Assert.Equal("BABBC", vm.SelectedCode);
        Assert.Equal(3.0, vm.ClockPosition);
        Assert.Equal(2, vm.Severity);   // bleibt erhalten (nicht mit null ueberschrieben)
        Assert.False(string.IsNullOrWhiteSpace(vm.Beschreibung));
    }

    // ── Fake ───────────────────────────────────────────────────────────────

    private sealed class FakeWorkbench : IAnnotationWorkbenchService
    {
        public WorkbenchSegmentation SegResult { get; set; } =
            new("rle", 100, 100, 1.0, "Maske erstellt.", Degraded: false);
        public WorkbenchSuggestion SugResult { get; set; } =
            new(new[] { new WorkbenchCodeCandidate("BAB", 0.9, "cls") }, FrameUsable: true, "ok", IsBend: false);
        public WorkbenchSaveResult SaveResult { get; set; } =
            new(true, null, "wb_1", "Indexed", "t_1");

        public List<WorkbenchDecision> SavedDecisions { get; } = new();
        public List<CancellationToken> SegmentTokens { get; } = new();
        public TaskCompletionSource? SegmentGate { get; set; }
        public Exception? SuggestThrows { get; set; }
        public Exception? SaveThrows { get; set; }

        public async Task<WorkbenchSegmentation> SegmentAsync(WorkbenchItem item, BoundingBox box, string codeHint, CancellationToken ct = default)
        {
            SegmentTokens.Add(ct);
            if (SegmentGate is not null)
                await SegmentGate.Task.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return SegResult;
        }

        public Task<WorkbenchSuggestion> SuggestAsync(WorkbenchItem item, BoundingBox box, CancellationToken ct = default)
            => SuggestThrows is not null ? Task.FromException<WorkbenchSuggestion>(SuggestThrows) : Task.FromResult(SugResult);

        public Task<WorkbenchSaveResult> SaveAsync(WorkbenchItem item, BoundingBox box, WorkbenchSegmentation? segmentation, WorkbenchDecision decision, CancellationToken ct = default)
        {
            if (SaveThrows is not null)
                return Task.FromException<WorkbenchSaveResult>(SaveThrows);
            SavedDecisions.Add(decision);
            return Task.FromResult(SaveResult);
        }
    }
}
