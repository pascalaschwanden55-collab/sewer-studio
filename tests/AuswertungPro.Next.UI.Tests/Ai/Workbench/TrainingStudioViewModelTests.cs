using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.Services;
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
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? ensureAiReady = null,
        Func<CancellationToken, Task<IReadOnlyList<PersonalGoldMainCodeStatus>>>? loadGoldProgress = null,
        ITrainingPreviewDetectionService? previewDetection = null)
        => new(
            wb,
            () => items ?? new[] { Foto() },
            "Pascal",
            labelLookup ?? (c => c),
            ensureAiReady,
            loadGoldProgress,
            previewDetection);

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
    public async Task BoxDrawn_ueberschreibt_keine_vorhandene_Handcodierung()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadItems(
        [
            Foto() with
            {
                ExistingCode = "BCAAA",
                ExistingBeschreibung = "Persoenlich gepruefter Anschluss"
            }
        ]);

        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.Equal("BCAAA", vm.SelectedCode);
        Assert.Equal("Persoenlich gepruefter Anschluss", vm.Beschreibung);
        Assert.NotNull(vm.Suggestion);
        Assert.NotNull(vm.Segmentation);
    }

    [Fact]
    public async Task FotoMitKiPruefen_analysiert_das_ganze_Foto_ohne_Box_oder_Speichern()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(
            wb,
            labelLookup: code => code == "BAB" ? "Riss" : null);
        vm.LoadQueueCommand.Execute(null);

        await vm.FotoMitKiPruefenCommand.ExecuteAsync(null);

        var candidate = Assert.Single(vm.SuggestionCandidates);
        Assert.Equal("BAB", candidate.VsaCode);
        Assert.Equal("Riss", candidate.Klartext);
        Assert.Equal(1, wb.SuggestPhotoCalls);
        Assert.Equal(0, wb.SegmentCalls);
        Assert.Null(vm.CurrentBox);
        Assert.Null(vm.Segmentation);
        Assert.True(string.IsNullOrWhiteSpace(vm.SelectedCode));
        Assert.Empty(wb.SavedDecisions);
        Assert.Contains("Nichts gespeichert", vm.StatusText);
    }

    [Fact]
    public async Task FotoMitKiPruefen_veraendert_keine_Handdaten()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        var handBox = vm.CurrentBox;
        var handMaske = vm.Segmentation;
        vm.SelectedCode = "BCAAA";
        vm.Beschreibung = "Persoenlich gepruefter Anschluss bei 9 Uhr";
        wb.SugResult = new WorkbenchSuggestion(
            new[] { new WorkbenchCodeCandidate("BBA", 0.8, "cls") },
            FrameUsable: true,
            "ok",
            IsBend: false);

        await vm.FotoMitKiPruefenCommand.ExecuteAsync(null);

        Assert.Equal(handBox, vm.CurrentBox);
        Assert.Equal(handMaske, vm.Segmentation);
        Assert.Equal("BCAAA", vm.SelectedCode);
        Assert.Equal("Persoenlich gepruefter Anschluss bei 9 Uhr", vm.Beschreibung);
        Assert.Empty(wb.SavedDecisions);
    }

    [Fact]
    public async Task FotoMitKiPruefen_ohne_geladenes_Modell_meldet_nicht_verfuegbar()
    {
        var wb = new FakeWorkbench
        {
            SugResult = new WorkbenchSuggestion(
                Array.Empty<WorkbenchCodeCandidate>(),
                FrameUsable: true,
                "ok",
                IsBend: false,
                ModelAvailable: false,
                UnavailableReason: "Testmodell fehlt.")
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        await vm.FotoMitKiPruefenCommand.ExecuteAsync(null);

        Assert.Contains("nicht verfuegbar", vm.StatusText);
        Assert.Contains("Testmodell fehlt", vm.StatusText);
        Assert.Empty(vm.SuggestionCandidates);
        Assert.Empty(wb.SavedDecisions);
    }

    [Fact]
    public async Task FotoMitKiPruefen_ohne_Foto_ruft_die_KI_nicht()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, items: Array.Empty<WorkbenchItem>());
        vm.LoadQueueCommand.Execute(null);

        await vm.FotoMitKiPruefenCommand.ExecuteAsync(null);

        Assert.Contains("zuerst ein Foto laden", vm.StatusText);
        Assert.Equal(0, wb.SuggestPhotoCalls);
    }

    [Fact]
    public async Task FotoMitKiPruefen_bei_nicht_erreichbarer_KI_erklaert_den_KI_Start()
    {
        var wb = new FakeWorkbench
        {
            SuggestThrows = new SidecarUnavailableException("localhost:8100 nicht erreichbar")
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        await vm.FotoMitKiPruefenCommand.ExecuteAsync(null);

        Assert.Contains("KI starten", vm.StatusText);
        Assert.Contains("Nichts gespeichert", vm.StatusText);
        Assert.DoesNotContain("localhost:8100", vm.StatusText);
        Assert.False(vm.IsBusy);
        Assert.Empty(wb.SavedDecisions);
    }

    [Fact]
    public async Task FotoMitKiPruefen_verwirft_spaetes_Ergebnis_nach_Bildwechsel()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wb = new FakeWorkbench { SuggestPhotoGate = gate };
        var vm = CreateVm(
            wb,
            items: [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);

        var photoCheck = vm.FotoMitKiPruefenCommand.ExecuteAsync(null);
        Assert.Equal(1, wb.SuggestPhotoCalls);

        vm.NextItemCommand.Execute(null);
        gate.SetResult();
        await photoCheck;

        Assert.Equal(@"C:\b.jpg", vm.CurrentImagePath);
        Assert.Null(vm.Suggestion);
        Assert.True(string.IsNullOrWhiteSpace(vm.SelectedCode));
        Assert.Contains("Bild 2", vm.StatusText);
        Assert.Empty(wb.SavedDecisions);
    }

    [Fact]
    public async Task BoxDrawn_startet_nicht_parallel_zur_allgemeinen_Foto_Pruefung()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wb = new FakeWorkbench { SuggestPhotoGate = gate };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);

        var photoCheck = vm.FotoMitKiPruefenCommand.ExecuteAsync(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.Equal(0, wb.SegmentCalls);
        Assert.Null(vm.CurrentBox);
        Assert.Contains("Foto-Pruefung", vm.StatusText);

        gate.SetResult();
        await photoCheck;
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
    public async Task Accept_waehrend_laufendem_Box_Lauf_speichert_nicht()
    {
        // Solange Segmentierung/Vorschlag laufen, ist Speichern gesperrt — sonst koennte
        // ein Entwurf ohne gepruefte Maske als vermeintlich fertiger Fund gespeichert werden.
        var wb = new FakeWorkbench
        {
            SegmentGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        var drawing = vm.BoxDrawnCommand.ExecuteAsync(TestBox);   // haengt am Gate, Box-Lauf aktiv
        Assert.Single(wb.SegmentTokens);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Empty(wb.SavedDecisions);          // SaveAsync wurde NICHT aufgerufen
        Assert.Contains("Segmentierung laeuft noch", vm.StatusText);
        Assert.Equal(0, vm.CurrentIndex);         // bleibt beim selben Item

        wb.SegmentGate.SetResult();
        await drawing;
    }

    [Fact]
    public async Task Accept_nach_fertigem_Box_Lauf_speichert_wieder()
    {
        // Nach dem Box-Lauf ist die Sperre aufgehoben — normaler Arbeitsfluss bleibt moeglich.
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Single(wb.SavedDecisions);
    }

    [Fact]
    public async Task Korrigieren_waehrend_laufendem_Speichern_wird_blockiert()
    {
        // Gemeinsamer Speicherschutz: ein zweites Akzeptieren/Korrigieren, waehrend der erste
        // Save noch laeuft, darf SaveAsync nicht erneut aufrufen (A/K-Doppeldruck).
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wb = new FakeWorkbench { SaveGate = gate };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        var first = vm.AcceptCommand.ExecuteAsync(null);      // haengt am Save-Gate
        await vm.CorrectCommand.ExecuteAsync(null);           // zweiter Druck -> blockiert

        Assert.Contains("Speichern laeuft bereits", vm.StatusText);

        gate.SetResult();
        await first;

        Assert.Single(wb.SavedDecisions);                     // SaveAsync lief nur einmal
    }

    [Fact]
    public async Task Standardmodell_bei_unqualifiziertem_Detektor_zeigt_Sperre_statt_Boxen()
    {
        // Phase 1: Das nachweislich kollabierte Altmodell darf keine blauen Boxen malen —
        // ehrliche Sperr-Meldung statt Vorschau; Auswahl und BCC-Pfad bleiben unberuehrt.
        var preview = new FakePreviewDetectionService
        {
            Qualification = new TrainingDetectorQualification(false, "BBox-Kollaps")
        };
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions[0];   // Aktives Standardmodell

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(0, preview.DetectCallCount);              // kein Vorschaulauf
        Assert.Empty(vm.PreviewDetections);                    // keine blauen Boxen
        Assert.Contains("gesperrt", vm.PreviewDetectionSummary);
        Assert.Contains("nicht freigegeben", vm.PreviewModelOptions[0].DisplayName);
        Assert.Equal(TrainingPreviewModelKind.ActiveStandard, vm.SelectedPreviewModel!.Kind);
    }

    [Fact]
    public async Task Standardmodell_bei_qualifiziertem_Detektor_laeuft_wie_bisher()
    {
        var preview = new FakePreviewDetectionService
        {
            Qualification = new TrainingDetectorQualification(true, null)
        };
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions[0];

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(1, preview.DetectCallCount);
        Assert.Single(vm.PreviewDetections);
        Assert.Equal("Aktives Standardmodell", vm.PreviewModelOptions[0].DisplayName);
    }

    [Fact]
    public async Task Bcc_Testmodell_bleibt_bei_unqualifiziertem_Detektor_nutzbar()
    {
        var preview = new FakePreviewDetectionService
        {
            Qualification = new TrainingDetectorQualification(false, "BBox-Kollaps")
        };
        var vm = CreateVm(
            new FakeWorkbench(),
            labelLookup: code => code == "BCC" ? "Bogen" : null,
            previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(1, preview.DetectCallCount);              // BCC-Pfad unveraendert
        Assert.Single(vm.PreviewDetections);
    }

    [Fact]
    public async Task Standardmodell_bei_unbekannter_Qualifikation_bleibt_gesperrt()
    {
        // Sidecar aus oder aelterer Sidecar ohne das Feld: keine blauen Boxen
        // ohne nachweislich positive Modellfreigabe.
        var preview = new FakePreviewDetectionService { Qualification = null };
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions[0];

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(0, preview.DetectCallCount);
        Assert.Empty(vm.PreviewDetections);
        Assert.Contains("gesperrt", vm.PreviewDetectionSummary);
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
    public void LoadItems_uebernimmt_Code_und_Beschreibung_des_unvollstaendigen_Goldframes()
    {
        var vm = CreateVm(new FakeWorkbench());
        var item = Foto(@"C:\gold.jpg") with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BCAAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Anschluss"
        };

        vm.LoadItems([item]);

        Assert.Equal("BCAAA", vm.SelectedCode);
        Assert.Equal("Persoenlich bestaetigter Anschluss", vm.Beschreibung);
    }

    [Fact]
    public void LoadItems_zeigt_Hauptcode_Ordnerhinweis_ohne_ihn_als_Code_zu_uebernehmen()
    {
        var vm = CreateVm(
            new FakeWorkbench(),
            labelLookup: code => code == "BAB" ? "Riss" : null);
        var item = Foto(@"C:\inbox\BAB\riss.jpg") with { SuggestedMainCode = "BAB" };

        vm.LoadItems([item]);

        Assert.True(string.IsNullOrWhiteSpace(vm.SelectedCode));
        Assert.Contains("Ordnerhinweis: BAB — Riss", vm.StatusText);
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
    public async Task Goldstand_wird_geladen_und_nach_erfolgreichem_Speichern_aktualisiert()
    {
        var calls = 0;
        var vm = CreateVm(
            new FakeWorkbench(),
            loadGoldProgress: _ =>
            {
                calls++;
                IReadOnlyList<PersonalGoldMainCodeStatus> result =
                [
                    new("BAB", 30, 30, 30, 30, 30, 50, 0, "ready")
                ];
                return Task.FromResult(result);
            });

        await vm.RefreshGoldProgressCommand.ExecuteAsync(null);

        Assert.Equal(1, calls);
        Assert.Single(vm.GoldProgressItems);
        Assert.Contains("1/1 Hauptcodes", vm.GoldProgressSummary);

        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Equal(2, calls);
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

    [Fact]
    public void ApplyCodeSelection_ueberschreibt_selbst_geschriebene_Beschreibung_nicht()
    {
        var vm = CreateVm(new FakeWorkbench());
        const string eigenerText = "Anschluss mit Formstueck bei 1 Uhr, etwa 150 mm hoch, verschlossen.";
        vm.Beschreibung = eigenerText;

        vm.ApplyCodeSelection("BCAAB", 1.0, severity: null);

        Assert.Equal("BCAAB", vm.SelectedCode);
        Assert.Equal(eigenerText, vm.Beschreibung);   // Nutzer-Text bleibt unangetastet
    }

    [Fact]
    public void ApplyCodeSelection_ersetzt_Platzhalter_durch_fertige_Katalogbeschreibung()
    {
        var vm = CreateVm(
            new FakeWorkbench(),
            labelLookup: code => code == "BCC" ? "Bogen" : null);
        vm.Beschreibung = "Riss bei 3 Uhr — Ausmass ergaenzen";   // alte, unbearbeitete Vorlage

        vm.ApplyCodeSelection("BCC", null, null, "Bogen");

        Assert.Equal("BCC", vm.SelectedCode);
        Assert.Equal("BCC — Bogen", vm.Beschreibung);
        Assert.False(GoldBeschreibungGuard.IsPlaceholder(vm.Beschreibung));
        Assert.True(vm.Beschreibung.Length >= 10);
    }

    [Fact]
    public async Task Persoenliche_Katalogcodierung_wird_ohne_Platzhalter_zum_Speichern_weitergegeben()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(
            wb,
            labelLookup: code => code == "BCD" ? "Rohranfang" : null);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        vm.ApplyCodeSelection("BCD", null, null, "Rohranfang");
        await vm.AcceptCommand.ExecuteAsync(null);

        var decision = Assert.Single(wb.SavedDecisions);
        Assert.Equal("BCD", decision.VsaCode);
        Assert.Equal("BCD — Rohranfang", decision.Beschreibung);
        Assert.False(GoldBeschreibungGuard.IsPlaceholder(decision.Beschreibung));
    }

    [Fact]
    public async Task Bcc_Testmodell_zeigt_nur_blaue_Vorschau_und_erzeugt_keine_Hand_Box()
    {
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(
            new FakeWorkbench(),
            labelLookup: code => code == "BCC" ? "Bogen" : null,
            previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        var detection = Assert.Single(vm.PreviewDetections);
        Assert.Equal(TrainingPreviewModelKind.BccTestCandidate, preview.LastModelKind);
        Assert.Contains("BCC", detection.DisplayText);
        Assert.Contains("Bogen", detection.DisplayText);
        Assert.Null(vm.CurrentBox);
        Assert.Null(vm.Segmentation);
        Assert.Contains("nicht gespeichert", vm.PreviewDetectionSummary);
    }

    [Fact]
    public async Task Bildwechsel_entfernt_automatische_Vorschau_Boxen()
    {
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(
            new FakeWorkbench(),
            items: [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")],
            previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);
        Assert.Single(vm.PreviewDetections);

        vm.NextItemCommand.Execute(null);

        Assert.Empty(vm.PreviewDetections);
        Assert.Equal(@"C:\b.jpg", vm.CurrentImagePath);
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
        public WorkbenchSuggestion BauartResult { get; set; } =
            new(new[] { new WorkbenchCodeCandidate("BCAAA", 0.8, "bca") }, FrameUsable: true, "", IsBend: false);
        public bool BauartVerfuegbar { get; set; } = true;
        public bool BcaBauartVerfuegbar => BauartVerfuegbar;

        public List<WorkbenchDecision> SavedDecisions { get; } = new();
        public List<CancellationToken> SegmentTokens { get; } = new();
        public int SegmentCalls { get; private set; }
        public int SuggestPhotoCalls { get; private set; }
        public TaskCompletionSource? SegmentGate { get; set; }
        public TaskCompletionSource? SuggestPhotoGate { get; set; }
        public TaskCompletionSource? SaveGate { get; set; }
        public Exception? SuggestThrows { get; set; }
        public Exception? SaveThrows { get; set; }

        public async Task<WorkbenchSegmentation> SegmentAsync(WorkbenchItem item, BoundingBox box, string codeHint, CancellationToken ct = default)
        {
            SegmentCalls++;
            SegmentTokens.Add(ct);
            if (SegmentGate is not null)
                await SegmentGate.Task.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return SegResult;
        }

        public Task<WorkbenchSuggestion> SuggestAsync(WorkbenchItem item, BoundingBox box, CancellationToken ct = default)
            => SuggestPhotoAsync(item, ct);

        public async Task<WorkbenchSuggestion> SuggestPhotoAsync(
            WorkbenchItem item,
            CancellationToken ct = default)
        {
            SuggestPhotoCalls++;
            if (SuggestPhotoGate is not null)
                await SuggestPhotoGate.Task.ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (SuggestThrows is not null)
                throw SuggestThrows;
            return SugResult;
        }

        public Task<WorkbenchSuggestion> SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default)
            => Task.FromResult(BauartResult);

        public async Task<WorkbenchSaveResult> SaveAsync(WorkbenchItem item, BoundingBox box, WorkbenchSegmentation? segmentation, WorkbenchDecision decision, CancellationToken ct = default)
        {
            if (SaveThrows is not null)
                return await Task.FromException<WorkbenchSaveResult>(SaveThrows);
            if (SaveGate is not null)
                await SaveGate.Task.ConfigureAwait(false);
            SavedDecisions.Add(decision);
            return SaveResult;
        }
    }

    private sealed class FakePreviewDetectionService : ITrainingPreviewDetectionService
    {
        public TrainingPreviewModelKind? LastModelKind { get; private set; }
        public int DetectCallCount { get; private set; }
        public TrainingDetectorQualification? Qualification { get; set; } =
            new(Qualified: true, Reason: null);

        public Task<TrainingDetectorQualification?> GetDetectorQualificationAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Qualification);

        public Task<TrainingPreviewDetectionResult> DetectAsync(
            string framePath,
            TrainingPreviewModelKind modelKind,
            double confidenceThreshold = 0.25,
            CancellationToken cancellationToken = default)
        {
            DetectCallCount++;
            LastModelKind = modelKind;
            IReadOnlyList<TrainingPreviewDetection> detections =
            [
                new(10, 20, 110, 140, "BCC_bogen", 0.91)
            ];
            return Task.FromResult(new TrainingPreviewDetectionResult(
                Available: true,
                Error: null,
                modelKind,
                ModelName: "bcc_bogen_test",
                ModelSha256: "abc",
                detections,
                InferenceTimeMs: 12));
        }
    }
}
