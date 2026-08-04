using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
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
        ITrainingPreviewDetectionService? previewDetection = null,
        IGoldQualityReviewQueueUseCase? goldQualityReview = null)
        => new(
            wb,
            () => items ?? new[] { Foto() },
            "Pascal",
            labelLookup ?? (c => c),
            ensureAiReady,
            loadGoldProgress,
            previewDetection,
            goldQualityReview);

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
    public async Task Speichern_startet_nicht_parallel_zur_allgemeinen_Foto_Pruefung()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        wb.SuggestPhotoGate = gate;

        var photoCheck = vm.FotoMitKiPruefenCommand.ExecuteAsync(null);
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Empty(wb.SavedDecisions);
        Assert.Contains("KI-Schritt", vm.StatusText);

        gate.SetResult();
        await photoCheck;
    }

    [Fact]
    public async Task Accept_speichert_ohne_Korrektur_und_laesst_das_Bild_fuer_die_Entscheidung_offen()
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
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\a.jpg", vm.CurrentImagePath);
        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.SavedEventCountForCurrentImage);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Contains("Weiteres Ereignis", vm.StatusText);
        Assert.Contains("Bild fertig", vm.StatusText);
    }

    [Fact]
    public async Task Weiteres_Ereignis_speichert_ein_zweites_ManualCoding_Objekt_auf_demselben_PDF_Bild()
    {
        var pdfReference = new WorkbenchSourceSuggestion(
            "BAIAB",
            "Dichtring eingeragt oberhalb Rohrmitte",
            "haltung.pdf",
            new string('a', 64),
            7,
            "12",
            "photo_id");
        var first = Foto(@"C:\pdf\photo-12.png") with
        {
            MeterStart = 4.2,
            MeterEnd = 8.4,
            IsStreckenschaden = true,
            SourceSuggestion = pdfReference,
        };
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [first, Foto(@"C:\next.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);
        vm.AddAnotherEventCommand.Execute(null);

        Assert.False(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.SavedEventCountForCurrentImage);
        Assert.Equal(@"C:\pdf\photo-12.png", vm.CurrentImagePath);
        Assert.False(vm.HasSourceSuggestion);
        Assert.Null(vm.CurrentBox);
        Assert.Null(vm.Segmentation);
        Assert.Null(vm.SelectedCode);
        Assert.Equal(string.Empty, vm.Beschreibung);

        var secondBox = new BoundingBox(0.25, 0.35, 0.15, 0.18);
        await vm.BoxDrawnCommand.ExecuteAsync(secondBox);
        vm.SelectedCode = "BABAA";
        vm.Beschreibung = "Zweiter sichtbarer Riss im selben Foto";
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Equal(2, wb.SavedItems.Count);
        Assert.Same(first, wb.SavedItems[0]);
        var additionalItem = wb.SavedItems[1];
        Assert.Equal(first.FramePath, additionalItem.FramePath);
        Assert.Equal(first.CaseId, additionalItem.CaseId);
        Assert.Equal(first.MeterStart, additionalItem.MeterStart);
        Assert.Equal(first.MeterStart, additionalItem.MeterEnd);
        Assert.False(additionalItem.IsStreckenschaden);
        Assert.Null(additionalItem.ExistingSampleId);
        Assert.Null(additionalItem.SourceSuggestion);
        Assert.Equal(new string('c', 64), additionalItem.ExpectedImageSha256);
        Assert.Equal(2, vm.SavedEventCountForCurrentImage);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.True(vm.IsAwaitingImageCompletion);

        vm.FinishImageCommand.Execute(null);

        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(@"C:\next.jpg", vm.CurrentImagePath);
        Assert.False(vm.IsAwaitingImageCompletion);
        Assert.Equal(0, vm.SavedEventCountForCurrentImage);
    }

    [Fact]
    public async Task Fertiges_Bild_kann_nicht_erneut_geoeffnet_oder_doppelt_gezaehlt_werden()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        vm.FinishImageCommand.Execute(null);

        await vm.PreviousItemCommand.ExecuteAsync(null);
        var selected = await vm.SelectQueueItemAsync(0);

        Assert.False(selected);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(@"C:\b.jpg", vm.CurrentImagePath);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Single(wb.SavedDecisions);
        Assert.Contains("abgeschlossen", vm.StatusText);
    }

    [Fact]
    public async Task Fertiger_Normaler_Reparaturfall_bleibt_nach_dem_Weitersprung_gesperrt()
    {
        var repair = Foto(@"C:\repair.jpg") with
        {
            ExistingSampleId = "sample-1",
            ExistingCode = "BABAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
        };
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadItems([repair, Foto(@"C:\next.jpg")]);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);

        await vm.PreviousItemCommand.ExecuteAsync(null);
        var selected = await vm.SelectQueueItemAsync(0);

        Assert.False(selected);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Single(wb.SavedDecisions);
        Assert.Contains("abgeschlossen", vm.StatusText);
    }

    [Fact]
    public async Task Neue_Warteschlange_hat_keine_Sperren_der_vorherigen_Liste()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        vm.FinishImageCommand.Execute(null);

        var loaded = vm.LoadItems([Foto(@"C:\neu.jpg")]);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.True(loaded);
        Assert.Equal(@"C:\neu.jpg", vm.CurrentImagePath);
        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(2, wb.SavedDecisions.Count);
    }

    [Fact]
    public async Task Mehrere_PDF_Operateurbefunde_desselben_Fotos_werden_zuerst_einzeln_geprueft()
    {
        var firstReference = new WorkbenchSourceSuggestion(
            "BAIAB", "Dichtring eingeragt", "haltung.pdf", new string('a', 64), 7, "12", "photo_id");
        var secondReference = new WorkbenchSourceSuggestion(
            "BABAA", "Riss sichtbar", "haltung.pdf", new string('a', 64), 7, "12", "photo_id");
        var first = Foto(@"C:\pdf\photo-12.png") with { SourceSuggestion = firstReference };
        var second = Foto(@"C:\pdf\photo-12.png") with { SourceSuggestion = secondReference };
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [first, second, Foto(@"C:\next.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.False(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(second.SourceSuggestion, vm.CurrentItem?.SourceSuggestion);
        Assert.Equal(new string('c', 64), vm.CurrentItem?.ExpectedImageSha256);
        Assert.Equal(1, vm.QueueDoneCount);

        await vm.BoxDrawnCommand.ExecuteAsync(new BoundingBox(0.3, 0.3, 0.2, 0.2));
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(2, vm.SavedEventCountForCurrentImage);
        Assert.Equal(1, vm.QueueDoneCount);
    }

    [Fact]
    public async Task PDF_Referenzen_desselben_Fotos_bleiben_auch_nach_Thumbnail_Sprung_zusammen()
    {
        var firstReference = new WorkbenchSourceSuggestion(
            "BAIAB", "Dichtring eingeragt", "haltung.pdf", new string('a', 64), 7, "12", "photo_id");
        var secondReference = new WorkbenchSourceSuggestion(
            "BABAA", "Riss sichtbar", "haltung.pdf", new string('a', 64), 7, "12", "photo_id");
        var first = Foto(@"C:\pdf\photo-12.png") with { SourceSuggestion = firstReference };
        var second = Foto(@"C:\pdf\photo-12.png") with { SourceSuggestion = secondReference };
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [first, second, Foto(@"C:\next.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        Assert.True(await vm.SelectQueueItemAsync(1));
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.False(vm.IsAwaitingImageCompletion);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(first.SourceSuggestion, vm.CurrentItem?.SourceSuggestion);
        Assert.Equal(1, vm.QueueDoneCount);

        await vm.BoxDrawnCommand.ExecuteAsync(new BoundingBox(0.3, 0.3, 0.2, 0.2));
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(2, vm.SavedEventCountForCurrentImage);
        Assert.Equal(0, vm.CurrentIndex);
    }

    [Fact]
    public async Task Ereigniszaehler_vermischt_gleichen_Bildpfad_nicht_zwischen_Haltungen()
    {
        var first = Foto(@"C:\shared\gold.png") with { CaseId = "haltung-a" };
        var second = Foto(@"C:\shared\gold.png") with { CaseId = "haltung-b" };
        var vm = CreateVm(new FakeWorkbench(), [first, second]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        vm.FinishImageCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.SavedEventCountForCurrentImage);
        Assert.Equal(1, vm.QueueDoneCount);
    }

    [Fact]
    public async Task Offene_Bildentscheidung_sperrt_Doppelspeichern_und_Thumbnailwechsel()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);

        await vm.AcceptCommand.ExecuteAsync(null);
        var selected = await vm.SelectQueueItemAsync(1);

        Assert.False(selected);
        Assert.Single(wb.SavedDecisions);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Contains("Bild fertig", vm.StatusText);
    }

    [Fact]
    public async Task Pfeil_rechts_beendet_ein_Bild_nicht_still_im_Hintergrund()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);

        await vm.NextItemCommand.ExecuteAsync(null);

        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Equal(@"C:\a.jpg", vm.CurrentImagePath);
        Assert.Contains("ausdruecklich", vm.StatusText);
        Assert.Contains("Bild fertig", vm.StatusText);
    }

    [Fact]
    public async Task Offene_Bildentscheidung_sperrt_Codewahl_und_Modelltest()
    {
        var preview = new FakePreviewDetectionService();
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        var savedCode = vm.SelectedCode;

        vm.SelectCodeCommand.Execute("BAF");
        vm.ApplyCodeSelection("BBC", 3, 2, "Ablagerung");
        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(savedCode, vm.SelectedCode);
        Assert.Equal(0, preview.DetectCallCount);
        Assert.Contains("Bild fertig", vm.StatusText);
    }

    [Fact]
    public async Task Verwerfen_eines_begonnenen_Zusatzereignisses_kehrt_zur_Bildentscheidung_zurueck()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        vm.AddAnotherEventCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(new BoundingBox(0.2, 0.3, 0.1, 0.1));

        vm.DiscardCommand.Execute(null);

        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.SavedEventCountForCurrentImage);
        Assert.Null(vm.CurrentBox);
        Assert.Single(wb.SavedDecisions);
        Assert.Contains("bleibt erhalten", vm.StatusText);

        vm.FinishImageCommand.Execute(null);

        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Equal(@"C:\b.jpg", vm.CurrentImagePath);
    }

    [Fact]
    public async Task Begonnenes_Zusatzereignis_sperrt_Bild_und_Warteschlangenwechsel()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        vm.AddAnotherEventCommand.Execute(null);

        await vm.NextItemCommand.ExecuteAsync(null);
        var selected = await vm.SelectQueueItemAsync(1);
        var queueChanged = vm.LoadItems([Foto(@"C:\fremd.jpg")]);

        Assert.False(selected);
        Assert.False(queueChanged);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\a.jpg", vm.CurrentImagePath);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Contains("speichern oder verwerfen", vm.StatusText);
    }

    [Fact]
    public async Task Gespeicherter_Entwurf_oeffnet_keine_Mehrfachentscheidung_und_zaehlt_nicht()
    {
        var wb = new FakeWorkbench
        {
            SaveResult = new WorkbenchSaveResult(
                true,
                "Entwurf gespeichert: Maske ist nicht goldfaehig.",
                "draft-1",
                "Entwurf",
                null,
                GoldApproved: false),
        };
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.False(vm.IsAwaitingImageCompletion);
        Assert.Equal(0, vm.SavedEventCountForCurrentImage);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Contains("Noch nicht als Gold", vm.StatusText);

        vm.DiscardCommand.Execute(null);

        Assert.Contains("Entwurf", vm.StatusText);
        Assert.Contains("Unvollstaendige Goldframes", vm.StatusText);
    }

    [Fact]
    public async Task Neuer_Entwurf_bleibt_an_dieselbe_SampleId_gebunden_und_kann_danach_Mehrfachobjekte_erfassen()
    {
        var wb = new FakeWorkbench();
        wb.SaveResults.Enqueue(new WorkbenchSaveResult(
            true,
            "Entwurf gespeichert: Maske ist noch nicht goldfaehig.",
            "draft-1",
            "Entwurf",
            null,
            GoldApproved: false,
            StoredImageSha256: new string('d', 64),
            StoredConfirmedAtUtc: new DateTimeOffset(2026, 8, 4, 8, 0, 0, TimeSpan.Zero)));
        wb.SaveResults.Enqueue(new WorkbenchSaveResult(
            true,
            null,
            "draft-1",
            "Indexed",
            "teacher-1",
            GoldApproved: true,
            StoredImageSha256: new string('d', 64)));
        var vm = CreateVm(wb, [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Equal("draft-1", vm.CurrentItem?.ExistingSampleId);
        Assert.Equal(new string('d', 64), vm.CurrentItem?.ExpectedImageSha256);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 4, 8, 0, 0, TimeSpan.Zero),
            vm.CurrentItem?.ExpectedConfirmedAtUtc);
        Assert.False(vm.IsAwaitingImageCompletion);

        await vm.BoxDrawnCommand.ExecuteAsync(new BoundingBox(0.45, 0.45, 0.25, 0.25));
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Equal(2, wb.SavedItems.Count);
        Assert.Equal("draft-1", wb.SavedItems[1].ExistingSampleId);
        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.SavedEventCountForCurrentImage);
        Assert.Equal(0, vm.QueueDoneCount);
    }

    [Fact]
    public async Task Verwerfen_nach_persistiertem_Zusatzentwurf_erklaert_dass_der_Draft_erhalten_bleibt()
    {
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);
        vm.AddAnotherEventCommand.Execute(null);
        wb.SaveResult = new WorkbenchSaveResult(
            true,
            "Entwurf gespeichert.",
            "draft-extra",
            "Entwurf",
            null,
            GoldApproved: false,
            StoredImageSha256: new string('c', 64),
            StoredConfirmedAtUtc: new DateTimeOffset(2026, 8, 4, 9, 0, 0, TimeSpan.Zero));
        await vm.BoxDrawnCommand.ExecuteAsync(new BoundingBox(0.2, 0.2, 0.1, 0.1));
        await vm.AcceptCommand.ExecuteAsync(null);

        vm.DiscardCommand.Execute(null);

        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.SavedEventCountForCurrentImage);
        Assert.Contains("Entwurf", vm.StatusText);
        Assert.Contains("Unvollstaendige Goldframes", vm.StatusText);
    }

    [Fact]
    public async Task Normal_geladener_Reparaturfall_mit_SampleId_bleibt_Einzelobjekt_und_springt_weiter()
    {
        var repair = Foto(@"C:\repair.jpg") with
        {
            ExistingSampleId = "sample-1",
            ExistingCode = "BABAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
        };
        var wb = new FakeWorkbench();
        var vm = CreateVm(wb);
        vm.LoadItems([repair, Foto(@"C:\next.jpg")]);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.False(vm.IsAwaitingImageCompletion);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(@"C:\next.jpg", vm.CurrentImagePath);
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
        Assert.Equal(0, vm.CurrentIndex);
        Assert.True(vm.IsAwaitingImageCompletion);
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
    public async Task Neue_Box_waehrend_laufendem_Speichern_wird_blockiert()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wb = new FakeWorkbench { SaveGate = gate };
        var vm = CreateVm(wb);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        var segmentCallsBeforeSave = wb.SegmentCalls;

        var saving = vm.AcceptCommand.ExecuteAsync(null);
        await vm.BoxDrawnCommand.ExecuteAsync(new BoundingBox(0.4, 0.4, 0.1, 0.1));

        Assert.Equal(segmentCallsBeforeSave, wb.SegmentCalls);
        Assert.Contains("Speichern laeuft", vm.StatusText);

        gate.SetResult();
        await saving;
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
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(1, preview.DetectCallCount);              // BCC-Pfad unveraendert
        Assert.Equal("bcc_bogen_b50b37ab8a4f", preview.LastCandidateId);
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
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions[0];

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(0, preview.DetectCallCount);
        Assert.Empty(vm.PreviewDetections);
        Assert.Contains("gesperrt", vm.PreviewDetectionSummary);
        Assert.Contains(
            vm.PreviewModelOptions,
            item => item.CandidateId == "bcc_bogen_b50b37ab8a4f");
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
    public async Task Segmentierungs_Warteschlange_segmentiert_vorhandene_Box_automatisch()
    {
        var workbench = new FakeWorkbench();
        var vm = CreateVm(workbench);
        var item = Foto(@"C:\gold.jpg") with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BCAAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Anschluss",
            ExistingBox = TestBox,
        };

        await vm.LoadSegmentationRepairItemsAsync([item]);

        Assert.Equal(1, workbench.SegmentCalls);
        Assert.Equal((BoundingBox?)TestBox, vm.CurrentBox);
        Assert.NotNull(vm.Segmentation);
        Assert.NotNull(vm.Suggestion);
        Assert.Empty(workbench.SavedDecisions);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_meldet_sichtbare_Maske_ausserhalb_der_Box_sofort_und_beim_Speichern()
    {
        var workbench = new FakeWorkbench
        {
            SegResult = new WorkbenchSegmentation(
                MaskRle: "1,8000",
                MaskImageWidth: 100,
                MaskImageHeight: 80,
                AreaPercent: 100,
                StatusText: "Maske erstellt.",
                Degraded: false,
                MaskAreaPixels: 8000),
        };
        var vm = CreateVm(workbench);
        var item = Foto(@"C:\gold.jpg") with
        {
            ExistingSampleId = "wb_alt",
            ExistingCode = "BAFCE",
            ExistingBeschreibung = "Persoenlich bestaetigter Oberflaechenschaden",
            ExistingBox = TestBox,
        };

        await vm.LoadSegmentationRepairItemsAsync([item]);

        Assert.NotNull(vm.Segmentation);
        Assert.Contains("sichtbar", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mindestens 80 %", vm.StatusText, StringComparison.Ordinal);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Empty(workbench.SavedDecisions);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Contains("mindestens 80 %", vm.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Maske fehlt", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_ohne_Box_zeigt_KI_Vorschlag_und_fordert_Box()
    {
        var workbench = new FakeWorkbench();
        var vm = CreateVm(workbench);

        await vm.LoadSegmentationRepairItemsAsync(
        [
            Foto(@"C:\gold.jpg") with
            {
                ExistingSampleId = "wb_alt",
                ExistingCode = "BCAAA",
                ExistingBeschreibung = "Persoenlich bestaetigter Anschluss",
            }
        ]);

        Assert.Equal(1, workbench.SuggestPhotoCalls);
        Assert.Equal(0, workbench.SegmentCalls);
        Assert.NotNull(vm.Suggestion);
        Assert.Null(vm.CurrentBox);
        Assert.Contains("Box", vm.StatusText);
        Assert.Empty(workbench.SavedDecisions);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_speichert_ohne_gueltige_Maske_nicht_als_erledigt()
    {
        var workbench = new FakeWorkbench();
        var vm = CreateVm(workbench);
        await vm.LoadSegmentationRepairItemsAsync(
        [
            Foto(@"C:\gold.jpg") with
            {
                ExistingSampleId = "wb_alt",
                ExistingCode = "BCAAA",
                ExistingBeschreibung = "Persoenlich bestaetigter Anschluss",
            }
        ]);
        vm.CurrentBox = TestBox;

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Empty(workbench.SavedDecisions);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Contains("gültige", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_springt_ohne_Akzeptieren_nicht_weiter()
    {
        var vm = CreateVm(new FakeWorkbench());
        await vm.LoadSegmentationRepairItemsAsync(
        [
            Foto(@"C:\first.jpg") with { ExistingSampleId = "wb_first" },
            Foto(@"C:\second.jpg") with { ExistingSampleId = "wb_second" },
        ]);

        await vm.NextItemCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\first.jpg", vm.CurrentImagePath);
        Assert.Contains("Akzeptieren", vm.StatusText);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_laesst_sich_auch_per_Vorschaubild_nicht_ueberspringen()
    {
        var vm = CreateVm(new FakeWorkbench());
        await vm.LoadSegmentationRepairItemsAsync(
        [
            Foto(@"C:\first.jpg") with { ExistingSampleId = "wb_first" },
            Foto(@"C:\second.jpg") with { ExistingSampleId = "wb_second" },
        ]);

        var selected = await vm.SelectQueueItemAsync(1);

        Assert.False(selected);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\first.jpg", vm.CurrentImagePath);
        Assert.Contains("Akzeptieren", vm.StatusText);
    }

    [Fact]
    public async Task Normale_Warteschlange_wechselt_per_Vorschaubild_das_Arbeitsbild()
    {
        var vm = CreateVm(
            new FakeWorkbench(),
            items: [Foto(@"C:\first.jpg"), Foto(@"C:\second.jpg")]);
        vm.LoadQueueCommand.Execute(null);

        var selected = await vm.SelectQueueItemAsync(1);

        Assert.True(selected);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(@"C:\second.jpg", vm.CurrentImagePath);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_bereitet_nach_Goldbestaetigung_das_naechste_Bild_vor()
    {
        var workbench = new FakeWorkbench
        {
            SegResult = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Maske erstellt.",
                Degraded: false,
                MaskAreaPixels: 1),
        };
        var vm = CreateVm(workbench);
        var first = Foto(@"C:\first.jpg") with
        {
            ExistingSampleId = "wb_first",
            ExistingCode = "BCAAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Anschluss",
            ExistingBox = TestBox,
        };
        var second = Foto(@"C:\second.jpg") with
        {
            ExistingSampleId = "wb_second",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
        };
        await vm.LoadSegmentationRepairItemsAsync([first, second]);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Single(workbench.SavedDecisions);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Equal(@"C:\second.jpg", vm.CurrentImagePath);
        Assert.Equal(2, workbench.SegmentCalls);
        Assert.NotNull(vm.Segmentation);
    }

    [Fact]
    public async Task Letzter_Segmentierungsfall_wird_geschlossen_und_kann_nicht_doppelt_gezaehlt_werden()
    {
        var workbench = new FakeWorkbench
        {
            SegResult = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Maske erstellt.",
                Degraded: false,
                MaskAreaPixels: 1),
        };
        var item = Foto(@"C:\only.jpg") with
        {
            ExistingSampleId = "wb_only",
            ExistingCode = "BABAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
        };
        var vm = CreateVm(workbench);
        await vm.LoadSegmentationRepairItemsAsync([item]);

        await vm.AcceptCommand.ExecuteAsync(null);
        await vm.AcceptCommand.ExecuteAsync(null);
        Assert.Contains("abgearbeitet", vm.StatusText);
        var reopened = await vm.SelectQueueItemAsync(0);

        Assert.Single(workbench.SavedDecisions);
        Assert.False(reopened);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Equal(1, vm.QueueTotalCount);
        Assert.Equal(-1, vm.CurrentIndex);
        Assert.Null(vm.CurrentImagePath);
        Assert.Contains("abgeschlossen", vm.StatusText);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_springt_nicht_zu_bereits_abgeschlossenem_Bild_zurueck()
    {
        var workbench = new FakeWorkbench
        {
            SegResult = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Maske erstellt.",
                Degraded: false,
                MaskAreaPixels: 1),
        };
        var first = Foto(@"C:\first.jpg") with
        {
            ExistingSampleId = "wb_first",
            ExistingCode = "BCAAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Anschluss",
            ExistingBox = TestBox,
        };
        var second = Foto(@"C:\second.jpg") with
        {
            ExistingSampleId = "wb_second",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
        };
        var vm = CreateVm(workbench);
        await vm.LoadSegmentationRepairItemsAsync([first, second]);
        await vm.AcceptCommand.ExecuteAsync(null);

        await vm.PreviousItemCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Equal(@"C:\second.jpg", vm.CurrentImagePath);
        Assert.Contains("abgeschlossen", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Segmentierungs_Warteschlange_bleibt_bei_gespeichertem_Entwurf_auf_dem_Bild()
    {
        var workbench = new FakeWorkbench
        {
            SegResult = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Maske erstellt.",
                Degraded: false,
                MaskAreaPixels: 1),
            SaveResult = new WorkbenchSaveResult(
                true,
                "Entwurf gespeichert: Maskenmaße passen nicht zum echten Bild.",
                "wb_first",
                "Entwurf",
                null,
                GoldApproved: false),
        };
        var vm = CreateVm(workbench);
        var first = Foto(@"C:\first.jpg") with
        {
            ExistingSampleId = "wb_first",
            ExistingCode = "BCAAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Anschluss",
            ExistingBox = TestBox,
        };
        var second = Foto(@"C:\second.jpg") with
        {
            ExistingSampleId = "wb_second",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
        };
        await vm.LoadSegmentationRepairItemsAsync([first, second]);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Single(workbench.SavedDecisions);
        Assert.Equal(0, vm.QueueDoneCount);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\first.jpg", vm.CurrentImagePath);
        Assert.Contains("bleibt", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bildwechsel_bricht_laufende_Boxsegmentierung_ab_und_verwirft_spaetes_Ergebnis()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workbench = new FakeWorkbench { SegmentGate = gate };
        var vm = CreateVm(
            workbench,
            items: [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")]);
        vm.LoadQueueCommand.Execute(null);

        var segmentierung = vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.NextItemCommand.ExecuteAsync(null);
        gate.SetResult();
        await segmentierung;

        Assert.Equal(@"C:\b.jpg", vm.CurrentImagePath);
        Assert.Null(vm.CurrentBox);
        Assert.Null(vm.Segmentation);
        Assert.Null(vm.Suggestion);
    }

    [Fact]
    public async Task Verwerfen_bricht_laufende_Boxsegmentierung_ab()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workbench = new FakeWorkbench { SegmentGate = gate };
        var vm = CreateVm(workbench);
        vm.LoadQueueCommand.Execute(null);

        var segmentierung = vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        vm.DiscardCommand.Execute(null);
        gate.SetResult();
        await segmentierung;

        Assert.Null(vm.CurrentBox);
        Assert.Null(vm.Segmentation);
        Assert.Null(vm.Suggestion);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Warteschlangenwechsel_waehrend_Speichern_bleibt_gesperrt_und_Save_schliesst_Reparaturfall()
    {
        var saveGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workbench = new FakeWorkbench
        {
            SegResult = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Maske erstellt.",
                Degraded: false,
                MaskAreaPixels: 1),
            SaveGate = saveGate,
        };
        var vm = CreateVm(workbench);
        var repair = Foto(@"C:\repair.jpg") with
        {
            ExistingSampleId = "wb_repair",
            ExistingCode = "BAB",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
        };
        await vm.LoadSegmentationRepairItemsAsync([repair]);

        var speichern = vm.AcceptCommand.ExecuteAsync(null);
        await workbench.SaveStarted.Task;
        var gewechselt = vm.LoadItems([Foto(@"C:\new.jpg")]);

        Assert.False(gewechselt);
        Assert.Equal(@"C:\repair.jpg", vm.CurrentImagePath);
        saveGate.SetResult();
        await speichern;

        Assert.Null(vm.CurrentImagePath);
        Assert.Equal(-1, vm.CurrentIndex);
        Assert.Equal(1, vm.QueueDoneCount);
        Assert.Contains("abgearbeitet", vm.StatusText);
    }

    [Fact]
    public async Task Pdf_Operateurvorgabe_wird_vorausgefuellt_und_als_Sam_Hinweis_verwendet()
    {
        var workbench = new FakeWorkbench();
        var vm = CreateVm(workbench);
        var source = new WorkbenchSourceSuggestion(
            "BCAAA",
            "Anschluss mit Formstueck bei 9 Uhr",
            "haltung.pdf",
            new string('a', 64),
            PageNumber: 2,
            PhotoId: "057",
            MatchKind: "same_block");

        vm.LoadItems([Foto(@"C:\pdf_review\foto.png") with { SourceSuggestion = source }]);
        Assert.Contains("PDF-Referenz: BCAAA, Seite 2, Foto 057", vm.StatusText);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.Equal("BCAAA", vm.SelectedCode);
        Assert.Equal("Anschluss mit Formstueck bei 9 Uhr", vm.Beschreibung);
        Assert.Equal("BCAAA", workbench.LastSegmentCodeHint);
    }

    [Fact]
    public async Task Pdf_Operateurcode_ist_beim_Speichern_die_Referenz_nicht_der_KI_Topcode()
    {
        var workbench = new FakeWorkbench
        {
            SugResult = new WorkbenchSuggestion(
                [new WorkbenchCodeCandidate("BAB", 0.95, "cls")],
                FrameUsable: true,
                "ok",
                IsBend: false)
        };
        var vm = CreateVm(workbench);
        var source = new WorkbenchSourceSuggestion(
            "BCAAA",
            "Anschluss mit Formstueck bei 9 Uhr",
            "haltung.pdf",
            new string('b', 64),
            PageNumber: 2,
            PhotoId: "057",
            MatchKind: "same_block");
        vm.LoadItems([Foto() with { SourceSuggestion = source }]);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        await vm.AcceptCommand.ExecuteAsync(null);

        var decision = Assert.Single(workbench.SavedDecisions);
        Assert.Equal("BCAAA", decision.VsaCode);
        Assert.False(decision.WasCorrected);
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
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(
            new FakeWorkbench(),
            ensureAiReady: (progress, _) =>
            {
                called = true;
                progress.Report("Vision-KI startet...");
                return Task.FromResult((true, "Vision-KI bereit. Foto laden und Box ziehen."));
            },
            previewDetection: preview);

        await vm.StartAiCommand.ExecuteAsync(null);

        Assert.True(called);
        Assert.Equal("Vision-KI bereit. Foto laden und Box ziehen.", vm.StatusText);
        Assert.Contains(
            vm.PreviewModelOptions,
            item => item.CandidateId == "bcc_bogen_b50b37ab8a4f");
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
    public async Task Bcc_Kandidaten_erscheinen_einzeln_ohne_automatische_Auswahl()
    {
        var preview = new FakePreviewDetectionService
        {
            Candidates =
            [
                new(
                    "bcc_bogen_b50b37ab8a4f",
                    "a" + new string('0', 63),
                    Map50: 0.74,
                    EpochsCompleted: 40,
                    CreatedUtc: "2026-07-28T14:43:21Z"),
                new(
                    "bcc_bogen_af8020b688ac_v3_negatives",
                    "b" + new string('0', 63),
                    Map50: 0.95,
                    EpochsCompleted: 40,
                    CreatedUtc: "2026-07-25T16:48:28Z"),
            ]
        };
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        Assert.DoesNotContain(
            vm.PreviewModelOptions,
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);

        var candidates = vm.PreviewModelOptions
            .Where(item => item.Kind == TrainingPreviewModelKind.BccTestCandidate)
            .ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CandidateId));
            Assert.False(string.IsNullOrWhiteSpace(item.CandidateSha256));
        });
        Assert.Equal(TrainingPreviewModelKind.ActiveStandard, vm.SelectedPreviewModel?.Kind);
    }

    [Fact]
    public async Task Kandidaten_Refresh_behaelt_nur_exakte_Id_und_Sha()
    {
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        preview.Candidates =
        [
            new(
                "bcc_bogen_b50b37ab8a4f",
                "b" + new string('0', 63),
                Map50: 0.75,
                EpochsCompleted: 41,
                CreatedUtc: "2026-07-29T10:00:00Z")
        ];
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);

        Assert.Equal(TrainingPreviewModelKind.ActiveStandard, vm.SelectedPreviewModel?.Kind);

        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);
        preview.Candidates =
        [
            new(
                "bcc_bogen_anderer_kandidat",
                "c" + new string('0', 63),
                Map50: 0.80,
                EpochsCompleted: 40,
                CreatedUtc: "2026-07-29T11:00:00Z")
        ];
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);

        Assert.Equal(TrainingPreviewModelKind.ActiveStandard, vm.SelectedPreviewModel?.Kind);
    }

    [Fact]
    public async Task Kandidaten_Refresh_ueberschreibt_keine_neue_Benutzerauswahl()
    {
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        preview.CatalogGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        await preview.CatalogRequestStarted.Task;
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.ActiveStandard);
        preview.CatalogGate.SetResult();
        await refresh;

        Assert.Equal(TrainingPreviewModelKind.ActiveStandard, vm.SelectedPreviewModel?.Kind);
    }

    [Fact]
    public async Task Fehlender_Kandidatenkatalog_entfernt_alte_Kandidaten()
    {
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);
        preview.CatalogAvailable = false;
        preview.CatalogError = "Katalog nicht lesbar";

        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);

        Assert.DoesNotContain(
            vm.PreviewModelOptions,
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);
        Assert.Equal(TrainingPreviewModelKind.ActiveStandard, vm.SelectedPreviewModel?.Kind);
        Assert.Empty(vm.PreviewDetections);
        Assert.Contains("Katalog nicht lesbar", vm.PreviewDetectionSummary);
    }

    [Fact]
    public async Task Bcc_Testmodell_ohne_exakte_Id_und_Sha_bleibt_gesperrt()
    {
        var preview = new FakePreviewDetectionService();
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        vm.SelectedPreviewModel = new TrainingStudioPreviewModelOption(
            TrainingPreviewModelKind.BccTestCandidate,
            "Unvollstaendiger Kandidat");

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Equal(0, preview.DetectCallCount);
        Assert.Empty(vm.PreviewDetections);
        Assert.Contains("gesperrt", vm.PreviewDetectionSummary);
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
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        var detection = Assert.Single(vm.PreviewDetections);
        Assert.Equal(TrainingPreviewModelKind.BccTestCandidate, preview.LastModelKind);
        Assert.Equal("bcc_bogen_b50b37ab8a4f", preview.LastCandidateId);
        Assert.Equal("a" + new string('0', 63), preview.LastCandidateSha256);
        Assert.Contains("BCC", detection.DisplayText);
        Assert.Contains("Bogen", detection.DisplayText);
        Assert.Null(vm.CurrentBox);
        Assert.Null(vm.Segmentation);
        Assert.Contains("nicht gespeichert", vm.PreviewDetectionSummary);
    }

    [Fact]
    public async Task Bcc_Testmodell_meldet_unbrauchbares_Foto_ohne_Negativbefund()
    {
        var preview = new FakePreviewDetectionService
        {
            FrameUsable = false,
            QualityReason = "zu dunkel",
        };
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        await vm.RunPreviewDetectionCommand.ExecuteAsync(null);

        Assert.Empty(vm.PreviewDetections);
        Assert.Contains("nicht geprueft", vm.PreviewDetectionSummary);
        Assert.Contains("zu dunkel", vm.PreviewDetectionSummary);
        Assert.DoesNotContain("kein Treffer", vm.PreviewDetectionSummary);
    }

    [Fact]
    public async Task Laufender_Modelltest_sperrt_Boxlauf_auch_nach_Bildwechsel()
    {
        var preview = new FakePreviewDetectionService
        {
            DetectionGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var workbench = new FakeWorkbench();
        var vm = CreateVm(
            workbench,
            items: [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg")],
            previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        var previewRun = vm.RunPreviewDetectionCommand.ExecuteAsync(null);
        await preview.DetectionStarted.Task;
        vm.NextItemCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.Equal(0, workbench.SegmentCalls);
        Assert.True(vm.IsBusy);
        Assert.Contains("Modelltest", vm.StatusText);

        preview.DetectionGate.SetResult();
        await previewRun;

        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Laufender_Modelltest_sperrt_Speichern()
    {
        var preview = new FakePreviewDetectionService
        {
            DetectionGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var workbench = new FakeWorkbench();
        var vm = CreateVm(workbench, previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        var previewRun = vm.RunPreviewDetectionCommand.ExecuteAsync(null);
        await preview.DetectionStarted.Task;
        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Empty(workbench.SavedDecisions);
        Assert.True(vm.IsBusy);
        Assert.Contains("Modelltest", vm.StatusText);

        preview.DetectionGate.SetResult();
        await previewRun;
    }

    [Fact]
    public async Task Laufende_Standardqualifikation_besitzt_den_Busy_Status()
    {
        var preview = new FakePreviewDetectionService
        {
            QualificationGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var workbench = new FakeWorkbench();
        var vm = CreateVm(workbench, previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);

        var previewRun = vm.RunPreviewDetectionCommand.ExecuteAsync(null);
        await preview.QualificationStarted.Task;
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);

        Assert.Equal(0, workbench.SegmentCalls);
        Assert.True(vm.IsBusy);

        preview.QualificationGate.SetResult();
        await previewRun;
    }

    [Fact]
    public async Task Kandidatenwechsel_verwirft_spaetes_Vorschauergebnis()
    {
        var preview = new FakePreviewDetectionService
        {
            DetectionGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var vm = CreateVm(new FakeWorkbench(), previewDetection: preview);
        vm.LoadQueueCommand.Execute(null);
        await vm.RefreshPreviewModelsCommand.ExecuteAsync(null);
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.BccTestCandidate);

        var run = vm.RunPreviewDetectionCommand.ExecuteAsync(null);
        await preview.DetectionStarted.Task;
        vm.SelectedPreviewModel = vm.PreviewModelOptions.Single(
            item => item.Kind == TrainingPreviewModelKind.ActiveStandard);
        preview.DetectionGate.SetResult();
        await run;

        Assert.Empty(vm.PreviewDetections);
        Assert.Contains("Aktives Standardmodell", vm.PreviewDetectionSummary);
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

    [Fact]
    public async Task Pfeil_rechts_am_letzten_Thumbnail_fuehrt_zum_ersten_noch_offenen_Bild()
    {
        var vm = CreateVm(
            new FakeWorkbench(),
            items: [Foto(@"C:\a.jpg"), Foto(@"C:\b.jpg"), Foto(@"C:\c.jpg")]);
        vm.LoadQueueCommand.Execute(null);
        Assert.True(await vm.SelectQueueItemAsync(2));

        await vm.NextItemCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(@"C:\a.jpg", vm.CurrentImagePath);
        Assert.DoesNotContain("abgearbeitet", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Goldpruefung_zeigt_gespeicherte_Maske_und_laesst_SAM_unveraendert()
    {
        var workbench = new FakeWorkbench();
        var segmentation = new WorkbenchSegmentation(
            "0,4050,1,3949",
            100,
            80,
            0.01,
            "Gespeicherte Goldmaske",
            Degraded: false,
            MaskAreaPixels: 1);
        var item = Foto(@"C:\gold-bab.jpg") with
        {
            ExistingSampleId = "gold-bab-1",
            ExistingCode = "BABAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
            ExistingSegmentation = segmentation,
            ExistingClockPosition = 9.5,
            ExistingSeverity = 3,
        };
        var loader = new FakeGoldQualityReviewUseCase(
            new GoldQualityReviewQueueResult(
                [item, item with { FramePath = @"C:\gold-baf.jpg", ExistingSampleId = "gold-baf-1" }],
                90,
                12,
                "session-1",
                Resumed: true));
        var vm = CreateVm(workbench, goldQualityReview: loader);

        await vm.LoadGoldQualityReviewCommand.ExecuteAsync(null);

        Assert.Equal(12, vm.QueueDoneCount);
        Assert.Equal(90, vm.QueueTotalCount);
        Assert.Equal((BoundingBox?)TestBox, vm.CurrentBox);
        Assert.Equal(segmentation, vm.Segmentation);
        Assert.Equal("BABAA", vm.SelectedCode);
        Assert.Equal(9.5, vm.ClockPosition);
        Assert.Equal(3, vm.Severity);
        Assert.Equal(0, workbench.SegmentCalls);
        Assert.Equal(1, workbench.SuggestPhotoCalls);
        Assert.NotNull(vm.Suggestion);
        Assert.Contains("Gespeicherte Goldmaske", vm.StatusText);
        Assert.False(await vm.SelectQueueItemAsync(1));
        Assert.Equal(@"C:\gold-bab.jpg", vm.CurrentImagePath);
    }

    [Fact]
    public async Task Offene_Mehrfachentscheidung_legt_keine_Goldpruefung_im_Hintergrund_an()
    {
        var loader = new FakeGoldQualityReviewUseCase(
            new GoldQualityReviewQueueResult([], 90, 0, "session-1", Resumed: false));
        var vm = CreateVm(new FakeWorkbench(), goldQualityReview: loader);
        vm.LoadItems([Foto(@"C:\a.jpg")]);
        await vm.BoxDrawnCommand.ExecuteAsync(TestBox);
        await vm.AcceptCommand.ExecuteAsync(null);

        await vm.LoadGoldQualityReviewCommand.ExecuteAsync(null);

        Assert.Equal(0, loader.ExecuteCalls);
        Assert.True(vm.IsAwaitingImageCompletion);
        Assert.Equal(@"C:\a.jpg", vm.CurrentImagePath);
        Assert.Contains("Bild fertig", vm.StatusText);
    }

    [Fact]
    public async Task Goldpruefung_zaehlt_erst_nach_Goldbestaetigung_und_erkennt_Codekorrektur()
    {
        var workbench = new FakeWorkbench();
        var item = Foto(@"C:\gold-bab.jpg") with
        {
            ExistingSampleId = "gold-bab-1",
            ExistingCode = "BABAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
            ExistingSegmentation = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Gespeicherte Goldmaske",
                Degraded: false,
                MaskAreaPixels: 1),
        };
        var loader = new FakeGoldQualityReviewUseCase(
            new GoldQualityReviewQueueResult([item], 90, 89, "session-1", Resumed: true));
        var vm = CreateVm(workbench, goldQualityReview: loader);
        await vm.LoadGoldQualityReviewCommand.ExecuteAsync(null);
        vm.SelectedCode = "BAFAA";

        await vm.AcceptCommand.ExecuteAsync(null);

        var decision = Assert.Single(workbench.SavedDecisions);
        Assert.True(decision.WasCorrected);
        Assert.Equal("BAFAA", decision.VsaCode);
        Assert.Equal(90, vm.QueueDoneCount);
        Assert.Equal(90, vm.QueueTotalCount);
        var completion = Assert.Single(loader.Completions);
        Assert.Equal("gold-bab-1", completion.SampleId);
        Assert.Equal(-1, vm.CurrentIndex);
        Assert.Null(vm.CurrentImagePath);

        await vm.AcceptCommand.ExecuteAsync(null);

        Assert.Single(workbench.SavedDecisions);
        Assert.Equal(90, vm.QueueDoneCount);
    }

    [Fact]
    public async Task Goldpruefung_Fehler_belaesst_die_bisherige_Warteschlange()
    {
        var loader = new FakeGoldQualityReviewUseCase(
            new InvalidOperationException("Schutzscan unvollstaendig"));
        var vm = CreateVm(new FakeWorkbench(), goldQualityReview: loader);
        vm.LoadItems([Foto(@"C:\bisher.jpg")]);

        await vm.LoadGoldQualityReviewCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\bisher.jpg", vm.CurrentImagePath);
        Assert.Single(vm.Items);
        Assert.Contains("bleibt erhalten", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Laufende_Goldpruefung_sperrt_einen_Warteschlangenwechsel()
    {
        var item = Foto(@"C:\gold.jpg") with
        {
            ExistingSampleId = "gold-1",
            ExistingCode = "BABAA",
            ExistingBeschreibung = "Persoenlich bestaetigter Riss",
            ExistingBox = TestBox,
            ExistingSegmentation = new WorkbenchSegmentation(
                "0,4050,1,3949",
                100,
                80,
                0.01,
                "Gespeicherte Goldmaske",
                Degraded: false,
                MaskAreaPixels: 1),
        };
        var loader = new FakeGoldQualityReviewUseCase(
            new GoldQualityReviewQueueResult([item], 90, 0, "session-1", Resumed: false))
        {
            Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var vm = CreateVm(new FakeWorkbench(), goldQualityReview: loader);
        vm.LoadItems([Foto(@"C:\bisher.jpg")]);

        var loading = vm.LoadGoldQualityReviewCommand.ExecuteAsync(null);
        await loader.Started.Task;
        var changed = vm.LoadItems([Foto(@"C:\fremd.jpg")]);

        Assert.False(changed);
        Assert.Equal(@"C:\bisher.jpg", vm.CurrentImagePath);
        loader.Gate.SetResult();
        await loading;
        Assert.Equal(@"C:\gold.jpg", vm.CurrentImagePath);
    }

    // ── Fake ───────────────────────────────────────────────────────────────

    private sealed class FakeGoldQualityReviewUseCase : IGoldQualityReviewQueueUseCase
    {
        private readonly GoldQualityReviewQueueResult? _result;
        private readonly Exception? _error;

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource? Gate { get; init; }

        public List<GoldQualityReviewCompletionRequest> Completions { get; } = new();

        public int ExecuteCalls { get; private set; }

        public FakeGoldQualityReviewUseCase(GoldQualityReviewQueueResult result)
            => _result = result;

        public FakeGoldQualityReviewUseCase(Exception error)
            => _error = error;

        public async Task<GoldQualityReviewQueueResult> ExecuteAsync(
            GoldQualityReviewQueueRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCalls++;
            Started.TrySetResult();
            if (Gate is not null)
                await Gate.Task;
            if (_error is not null)
                throw _error;
            return _result!;
        }

        public Task MarkCompletedAsync(
            GoldQualityReviewCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Completions.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkbench : IAnnotationWorkbenchService
    {
        public WorkbenchSegmentation SegResult { get; set; } =
            new("rle", 100, 100, 1.0, "Maske erstellt.", Degraded: false);
        public WorkbenchSuggestion SugResult { get; set; } =
            new(new[] { new WorkbenchCodeCandidate("BAB", 0.9, "cls") }, FrameUsable: true, "ok", IsBend: false);
        public WorkbenchSaveResult SaveResult { get; set; } =
            new(
                true,
                null,
                "wb_1",
                "Indexed",
                "t_1",
                GoldApproved: true,
                StoredImageSha256: new string('c', 64));
        public Queue<WorkbenchSaveResult> SaveResults { get; } = new();
        public WorkbenchSuggestion BauartResult { get; set; } =
            new(new[] { new WorkbenchCodeCandidate("BCAAA", 0.8, "bca") }, FrameUsable: true, "", IsBend: false);
        public bool BauartVerfuegbar { get; set; } = true;
        public bool BcaBauartVerfuegbar => BauartVerfuegbar;

        public List<WorkbenchDecision> SavedDecisions { get; } = new();
        public List<WorkbenchItem> SavedItems { get; } = new();
        public List<CancellationToken> SegmentTokens { get; } = new();
        public string? LastSegmentCodeHint { get; private set; }
        public int SegmentCalls { get; private set; }
        public int SuggestPhotoCalls { get; private set; }
        public TaskCompletionSource? SegmentGate { get; set; }
        public TaskCompletionSource? SuggestPhotoGate { get; set; }
        public TaskCompletionSource? SaveGate { get; set; }
        public TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? SuggestThrows { get; set; }
        public Exception? SaveThrows { get; set; }

        public async Task<WorkbenchSegmentation> SegmentAsync(WorkbenchItem item, BoundingBox box, string codeHint, CancellationToken ct = default)
        {
            SegmentCalls++;
            SegmentTokens.Add(ct);
            LastSegmentCodeHint = codeHint;
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
            SaveStarted.TrySetResult();
            if (SaveGate is not null)
                await SaveGate.Task.ConfigureAwait(false);
            SavedItems.Add(item);
            SavedDecisions.Add(decision);
            return SaveResults.Count > 0 ? SaveResults.Dequeue() : SaveResult;
        }
    }

    private sealed class FakePreviewDetectionService : ITrainingPreviewDetectionService
    {
        public TrainingPreviewModelKind? LastModelKind { get; private set; }
        public string? LastCandidateId { get; private set; }
        public string? LastCandidateSha256 { get; private set; }
        public int DetectCallCount { get; private set; }
        public TrainingDetectorQualification? Qualification { get; set; } =
            new(Qualified: true, Reason: null);
        public TaskCompletionSource DetectionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? DetectionGate { get; set; }
        public TaskCompletionSource CatalogRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? CatalogGate { get; set; }
        public TaskCompletionSource QualificationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? QualificationGate { get; set; }
        public bool CatalogAvailable { get; set; } = true;
        public string? CatalogError { get; set; }
        public bool FrameUsable { get; set; } = true;
        public string? QualityReason { get; set; }
        public IReadOnlyList<TrainingPreviewCandidateInfo> Candidates { get; set; } =
        [
            new(
                "bcc_bogen_b50b37ab8a4f",
                "a" + new string('0', 63),
                Map50: 0.74,
                EpochsCompleted: 40,
                CreatedUtc: "2026-07-28T14:43:21Z")
        ];

        public async Task<TrainingDetectorQualification?> GetDetectorQualificationAsync(
            CancellationToken cancellationToken = default)
        {
            if (QualificationGate is not null)
            {
                QualificationStarted.TrySetResult();
                await QualificationGate.Task;
            }
            return Qualification;
        }

        public async Task<TrainingPreviewCandidateCatalogResult> GetBccCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            if (CatalogGate is not null)
            {
                CatalogRequestStarted.TrySetResult();
                await CatalogGate.Task;
            }
            return new TrainingPreviewCandidateCatalogResult(
                Available: CatalogAvailable,
                Error: CatalogError,
                Candidates);
        }

        public Task<TrainingPreviewDetectionResult> DetectAsync(
            string framePath,
            TrainingPreviewModelKind modelKind,
            double confidenceThreshold = 0.25,
            CancellationToken cancellationToken = default)
            => DetectCoreAsync(modelKind, candidateId: null, candidateSha256: null);

        public Task<TrainingPreviewDetectionResult> DetectBccCandidateAsync(
            string framePath,
            string candidateId,
            string candidateSha256,
            double confidenceThreshold = 0.25,
            CancellationToken cancellationToken = default)
            => DetectCoreAsync(
                TrainingPreviewModelKind.BccTestCandidate,
                candidateId,
                candidateSha256);

        private async Task<TrainingPreviewDetectionResult> DetectCoreAsync(
            TrainingPreviewModelKind modelKind,
            string? candidateId,
            string? candidateSha256)
        {
            DetectCallCount++;
            LastModelKind = modelKind;
            LastCandidateId = candidateId;
            LastCandidateSha256 = candidateSha256;
            DetectionStarted.TrySetResult();
            if (DetectionGate is not null)
                await DetectionGate.Task;
            IReadOnlyList<TrainingPreviewDetection> detections =
            [
                new(10, 20, 110, 140, "BCC_bogen", 0.91)
            ];
            return new TrainingPreviewDetectionResult(
                Available: true,
                Error: null,
                modelKind,
                ModelName: candidateId ?? "bcc_bogen_test",
                ModelSha256: candidateSha256 ?? "abc",
                detections,
                InferenceTimeMs: 12,
                FrameUsable,
                QualityReason);
        }
    }
}
