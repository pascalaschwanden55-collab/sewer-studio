using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.UI.Views.Windows;

// Batch-Nachtbetrieb fuer das TrainingCenter: Voll-automatischer Selbsttraining-
// Lauf ueber alle Haltungen eines Ordners, optional mit YOLO-Auto-Retrain und
// Qwen-LoRA-Training. Aus dem Hauptdatei extrahiert (Slice 9a).
public partial class TrainingCenterWindow
{
    private async void StartBatchNightRun_Click(object sender, RoutedEventArgs e)
    {
        // Phase 5.1.B Etappe 3.K: via DI-Container.
        var diagnostics = App.Resolve<AuswertungPro.Next.Application.Diagnostics.DiagnosticsOptions>();
        var codeCatalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>();
        var winCanImport = App.Resolve<AuswertungPro.Next.Application.Import.IWinCanDbImportService>();
        var ibakImport = App.Resolve<AuswertungPro.Next.Application.Import.IIbakImportService>();
        var sidecarSvc = App.Resolve<Ai.PythonSidecarService>();
        var pipelineCfg = App.Resolve<AuswertungPro.Next.Application.Ai.PipelineConfig>();

        // pdftotext-Pfad aus den App-Einstellungen setzen
        AuswertungPro.Next.Infrastructure.Ai.Training.Services.PdfProtocolTableParser.PdfToTextExePath = diagnostics.ExplicitPdfToTextPath;

        var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        if (!cfg.Enabled) { _dialogs.ShowMessage("KI ist deaktiviert.", "Batch-Nachtbetrieb"); return; }

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner mit Haltungen waehlen" };
        if (dlg.ShowDialog() != true) return;

        var confirm = _dialogs.ShowMessage(
            $"Batch-Nachtbetrieb starten?\n\nOrdner: {dlg.FolderName}\n\nAlle Haltungen mit Video + PDF werden automatisch verarbeitet.\nDas kann mehrere Stunden dauern.",
            "Batch-Nachtbetrieb", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        // V4.2 Nachbesserung: Test-Cap fuer begrenzte Laeufe.
        var limitInput = Microsoft.VisualBasic.Interaction.InputBox(
            "Wie viele Haltungen verarbeiten?\n\n" +
            "  0 = alle Haltungen im Ordner (voller Batch)\n" +
            "  2 = empfohlen fuer ersten V4.2-Testlauf\n",
            "Batch-Umfang",
            "2");
        if (string.IsNullOrWhiteSpace(limitInput)) return;
        if (!int.TryParse(limitInput, out var maxHaltungen) || maxHaltungen < 0)
        {
            _dialogs.ShowMessage("Ungueltige Zahl — Abbruch.", "Batch-Nachtbetrieb");
            return;
        }

        var allowedSet = new System.Collections.Generic.HashSet<string>(
            codeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
        var plausibility = new AuswertungPro.Next.Application.Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var pipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, http);
        var protocolLoader = new Ai.Training.Services.ProtocolLoaderFactory(winCanImport, ibakImport);
        var meterTimeline = new AuswertungPro.Next.Infrastructure.Ai.Training.MeterTimelineService(cfg);
        var videoOrch = new AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);

        // V4.1: Batch-Pipeline fuer den initialen Orchestrator
        if (sidecarSvc.IsAvailable)
        {
            var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var sidecarClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);
            var ollamaClient = cfg.CreateOllamaClient();
            var qwenVision = new AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService(ollamaClient, cfg.VisionModel, cfg.ReferenceVisionModel, cfg.OllamaNumCtx);
            videoOrch.BatchPipeline = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.BatchPipelineService(
                sidecarClient, qwenVision, pipelineCfg,
                cfg.FfmpegPath ?? AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfmpeg());
        }

        Ai.KnowledgeBase.KbEnrichmentService enrichment;
        try
        {
            var kbHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var ollamaConfig = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();
            var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            var embedder = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.EmbeddingService(kbHttp, ollamaConfig);
            var retrieval = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.RetrievalService(kbCtx, embedder);
            var kbManager = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager(kbCtx, embedder);
            var dedup = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService(embedder, retrieval);
            // H3: Review-Queue fuer Mittel-Confidence-Samples (0.65 <= conf < 0.85).
            _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
            enrichment = new Ai.KnowledgeBase.KbEnrichmentService(
                kbManager, dedup, log: null, reviewQueue: _reviewQueueService);
        }
        catch (Exception ex) { _dialogs.ShowMessage($"KB-Fehler: {ex.Message}", "Knowledge-Base", MessageBoxButton.OK, MessageBoxImage.Error); return; }

        // Sidecar-Pfad fuer Auto-Restart bei Crash
        var sidecarDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "sidecar");
        if (!System.IO.Directory.Exists(sidecarDir))
            sidecarDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "sidecar");
        // Factory fuer parallele Pipeline-Instanzen: Jede Haltung bekommt ihren eigenen
        // HttpClient + Pipeline + Orchestrator (VideoSelfTrainingOrchestrator hat internen State)
        Func<AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator> orchestratorFactory = () =>
        {
            var pHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var pPipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, pHttp);
            var pTimeline = new AuswertungPro.Next.Infrastructure.Ai.Training.MeterTimelineService(cfg);
            var orch = new AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator(pPipeline, pTimeline);

            // V4.1: Batch-Pipeline (YOLO Batch → Filter → Qwen ×6 parallel)
            if (sidecarSvc.IsAvailable)
            {
                var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var sidecarClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);
                var ollamaClient = cfg.CreateOllamaClient();
                var qwenVision = new AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService(ollamaClient, cfg.VisionModel, cfg.ReferenceVisionModel, cfg.OllamaNumCtx);
                orch.BatchPipeline = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.BatchPipelineService(
                    sidecarClient, qwenVision, pipelineCfg,
                    cfg.FfmpegPath ?? AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfmpeg());
            }

            return orch;
        };

        // V4.2 Phase 1.4: UncertaintySamplingService auf den Fenster-lokalen ReviewQueueService mappen.
        _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
        var uncertaintySampler = new AuswertungPro.Next.Application.Ai.SelfImproving.UncertaintySamplingService(_reviewQueueService);

        var batchOrch = new Ai.Training.BatchSelfTrainingOrchestrator(
            videoOrch, protocolLoader, enrichment, sidecarDir: sidecarDir,
            orchestratorFactory: orchestratorFactory,
            uncertaintySampler: uncertaintySampler,
            sidecarUrl: pipelineCfg.SidecarUrl.ToString());
        var request = new AuswertungPro.Next.Application.Ai.Training.Models.BatchSelfTrainingRequest
        {
            ExportRootPath = dlg.FolderName,
            MaxHaltungen = maxHaltungen,
            // V4.3: Checkbox "Alle erneut durchlaufen" (oben beim Selbsttraining-Button) gilt auch hier.
            // Wenn aktiv → bereits verarbeitete Haltungen nochmals durchlaufen.
            SkipAlreadyProcessed = !Vm.ForceRerunAll
        };

        var btnBatch = this.FindName("BtnBatchNight") as System.Windows.Controls.Button;
        if (btnBatch is not null) { btnBatch.IsEnabled = false; btnBatch.Content = "Batch laeuft..."; }
        BtnBatchCancel.Visibility = Visibility.Visible;

        _batchCts?.Dispose();
        _batchCts = new System.Threading.CancellationTokenSource();

        if (TryCreateKnowledgeBaseSnapshot(out var snapshotInfo))
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [KB] Snapshot: {snapshotInfo}\n");
        else
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [KB] Snapshot uebersprungen: {snapshotInfo}\n");

        var sampleIdsBefore = new HashSet<string>(StringComparer.Ordinal);
        var trainingCountBefore = 0;
        var trainingBboxBefore = 0;
        try
        {
            var before = await TrainingSamplesStore.LoadAsync();
            trainingCountBefore = before.Count;
            trainingBboxBefore = before.Count(s => s.HasBbox);
            foreach (var sample in before)
            {
                if (!string.IsNullOrWhiteSpace(sample.SampleId))
                    sampleIdsBefore.Add(sample.SampleId);
            }
        }
        catch
        {
            // Training-Sample-Statistik ist optional.
        }

        Vm.LogText = ""; // Log leeren vor Batch-Start
        var progress = new Progress<AuswertungPro.Next.Application.Ai.Training.Models.BatchSelfTrainingProgress>(p =>
        {
            Vm.StatusText = p.Status;

            // Alles ins Live-Log schreiben damit der User sieht was passiert
            var ts = $"[{DateTime.Now:HH:mm:ss}]";
            var phase = p.Phase ?? "";
            Vm.AppendToLogText($"{ts} [{p.CurrentIndex}/{p.TotalHaltungen}] {p.Status}\n");

            // Gesamtstatistik nach Ergebnis-Zeilen
            if (p.RunningStats is { } s && phase == "Ergebnis")
            {
                Vm.AppendToLogText($"    Gesamt: F1={s.F1:P0} | TP:{s.TruePositives} FN:{s.FalseNegatives} FP:{s.FalsePositives} | KB:+{s.KbIndexed}\n");
            }

            // Log-Textbox automatisch nach unten scrollen
            LogTextBox?.ScrollToEnd();

            if (p.EstimatedRemaining.HasValue)
                Title = $"Training Center — Batch {p.CurrentIndex}/{p.TotalHaltungen} — {p.EstimatedRemaining.Value.Hours}h {p.EstimatedRemaining.Value.Minutes}min";
        });

        try
        {
            var result = await Task.Run(() => batchOrch.RunAsync(request, progress, _batchCts!.Token));

            // V4.2 Nachbesserung: Review-Queue nach Batch neu laden + Counter-Log ins UI.
            var queueCountBefore = Vm.ReviewQueueCount;
            Vm.LoadReviewQueue(_reviewQueueService);
            var queueDelta = Vm.ReviewQueueCount - queueCountBefore;
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [V4.2] Review-Queue: {queueDelta:+#;-#;0} Items, total {Vm.ReviewQueueCount}\n");

            var s = result.FinalStats;
            string retrainSummary;
            var trainingCountAfter = trainingCountBefore;
            var trainingBboxAfter = trainingBboxBefore;
            var newSampleCount = 0;
            var newVideoTimestampCount = 0;
            var newBboxCount = 0;
            double? newTimeMin = null;
            double? newTimeMax = null;
            var timeExamples = "n/a";

            try
            {
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [YOLO] Auto-Retrain Pruefung gestartet...\n");

                using var retrainHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(90) };
                var retrainClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, retrainHttp);
                var benchmarkSetStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkSetStore();
                var benchmarkMetricsStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkMetricsStore();
                var benchmarkRunner = new AuswertungPro.Next.Infrastructure.Ai.Training.BenchmarkRunner(
                    benchmarkSetStore,
                    benchmarkMetricsStore,
                    videoOrch,
                    protocolLoader.LoadProtocolAsync);

                var retrainOrchestrator = new AuswertungPro.Next.Infrastructure.Ai.Training.YoloRetrainOrchestrator(
                    retrainClient,
                    new AuswertungPro.Next.Infrastructure.Ai.Training.YoloDatasetExportService(),
                    benchmarkRunner,
                    benchmarkMetricsStore,
                    sidecarDir,
                    msg => Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [YOLO] {msg}\n"));

                var retrain = await retrainOrchestrator.RunIfEligibleAsync(ct: _batchCts!.Token);
                retrainSummary = retrain.Deployed
                    ? $"Deploy OK ({System.IO.Path.GetFileName(retrain.ActiveModelPath)}, F1={retrain.BenchmarkF1:P1})"
                    : retrain.StatusText;
            }
            catch (Exception retrainEx)
            {
                retrainSummary = $"Auto-Retrain Fehler: {retrainEx.Message}";
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [YOLO] {retrainSummary}\n");
            }

            // ── Phase 3: LoRA-Training (nach YOLO-Retrain) ──
            // V4.2: LoRA-Auto-Training default DEAKTIVIERT wegen OOM-Problem bei >9000 Samples.
            // Opt-in via Umgebungsvariable SEWERSTUDIO_ENABLE_LORA_AUTOTRAIN=1.
            string loraSummary;
            var loraEnabled = Environment.GetEnvironmentVariable("SEWERSTUDIO_ENABLE_LORA_AUTOTRAIN") == "1";
            if (!loraEnabled)
            {
                loraSummary = "LoRA-Auto-Training deaktiviert (SEWERSTUDIO_ENABLE_LORA_AUTOTRAIN nicht gesetzt)";
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] {loraSummary}\n");
            }
            else
            {
                try
                {
                    Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] Qwen LoRA-Training Pruefung...\n");

                    using var loraHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(120) };
                    var loraClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, loraHttp);
                    var ollamaConfig = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();

                    var loraBenchmarkSetStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkSetStore();
                    var loraBenchmarkMetricsStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkMetricsStore();
                    var loraBenchmarkRunner = new AuswertungPro.Next.Infrastructure.Ai.Training.BenchmarkRunner(
                        loraBenchmarkSetStore,
                        loraBenchmarkMetricsStore,
                        videoOrch,
                        protocolLoader.LoadProtocolAsync);

                    using var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
                    var loraOrchestrator = new AuswertungPro.Next.Infrastructure.Ai.Training.QwenLoraOrchestrator(
                        loraClient,
                        kbCtx,
                        ollamaConfig,
                        loraBenchmarkRunner,
                        loraBenchmarkMetricsStore,
                        msg => Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] {msg}\n"));

                    var loraResult = await loraOrchestrator.RunIfEligibleAsync(ct: _batchCts!.Token);
                    loraSummary = loraResult.Deployed
                        ? $"Deploy OK ({loraResult.ActiveModelName}, F1={loraResult.BenchmarkF1:P1})"
                        : loraResult.StatusText;
                }
                catch (Exception loraEx)
                {
                    loraSummary = $"LoRA Fehler: {loraEx.Message}";
                    Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] {loraSummary}\n");
                }
            }

            try
            {
                var after = await TrainingSamplesStore.LoadAsync();
                trainingCountAfter = after.Count;
                trainingBboxAfter = after.Count(s => s.HasBbox);

                var newSamples = after
                    .Where(sample => !string.IsNullOrWhiteSpace(sample.SampleId) && !sampleIdsBefore.Contains(sample.SampleId))
                    .ToList();

                newSampleCount = newSamples.Count;
                newVideoTimestampCount = newSamples.Count(sample =>
                    string.Equals(sample.SourceType, SourceTypeNames.VideoTimestamp, StringComparison.OrdinalIgnoreCase));
                newBboxCount = newSamples.Count(sample => sample.HasBbox);

                var times = newSamples
                    .Where(sample => sample.TimeSeconds > 0)
                    .Select(sample => sample.TimeSeconds)
                    .OrderBy(seconds => seconds)
                    .ToList();

                if (times.Count > 0)
                {
                    newTimeMin = times[0];
                    newTimeMax = times[^1];
                    var median = times[times.Count / 2];
                    timeExamples = $"{times[0]:F1}s, {median:F1}s, {times[^1]:F1}s";
                }

                var videoShare = newSampleCount > 0
                    ? (double)newVideoTimestampCount / newSampleCount
                    : 0.0;
                var timeRangeText = newTimeMin.HasValue
                    ? $"{newTimeMin.Value:F1}s..{newTimeMax!.Value:F1}s"
                    : "keine >0s";
                Vm.AppendToLogText(
                    $"[{DateTime.Now:HH:mm:ss}] [QA] Neu: {newSampleCount}, VideoTimestamp: {newVideoTimestampCount} ({videoShare:P1}), " +
                    $"BBox: {newBboxCount}, TimeSeconds: {timeRangeText}, Beispiele: {timeExamples}\n");
            }
            catch
            {
                // Training-Sample-Statistik ist optional.
            }

            var trainingDelta = Math.Max(0, trainingCountAfter - trainingCountBefore);
            var trainingBboxDelta = Math.Max(0, trainingBboxAfter - trainingBboxBefore);

            _dialogs.ShowMessage(
                $"Batch fertig: {result.Processed}/{result.TotalHaltungen} Haltungen in {result.TotalDuration.TotalMinutes:F0} Min\n\n" +
                $"TP:{s.TruePositives} FN:{s.FalseNegatives} FP:{s.FalsePositives} MM:{s.CodeMismatches}\n" +
                $"F1: {s.F1:P1}\n\nKB: +{s.KbIndexed} neu, {s.KbDeduplicated} Duplikate\n" +
                $"Training-Samples: +{trainingDelta} neu, davon +{trainingBboxDelta} mit BBox\n" +
                $"Neu seit Start: {newSampleCount}, VideoTimestamp: {newVideoTimestampCount}, BBox: {newBboxCount}\n" +
                $"TimeSeconds > 0: {(newTimeMin.HasValue ? $"{newTimeMin.Value:F1}s..{newTimeMax!.Value:F1}s ({timeExamples})" : "keine")}\n\n" +
                $"YOLO Auto-Retrain: {retrainSummary}\n" +
                $"Qwen LoRA: {loraSummary}",
                "Batch-Nachtbetrieb", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Batch abgebrochen.\n");
            _dialogs.ShowMessage("Batch-Nachtbetrieb wurde abgebrochen.", "Abgebrochen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { _dialogs.ShowMessage($"Batch-Fehler: {ex.Message}", "Batch-Nachtbetrieb", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally
        {
            var btnRestore = this.FindName("BtnBatchNight") as System.Windows.Controls.Button;
            if (btnRestore is not null) { btnRestore.IsEnabled = true; btnRestore.Content = "\U0001f319 Batch-Nachtbetrieb"; }
            BtnBatchCancel.Visibility = Visibility.Collapsed;
            BtnBatchCancel.IsEnabled = true;
            BtnBatchCancel.Content = "⛔ Abbrechen";
            _batchCts?.Dispose();
            _batchCts = null;
            Title = "Training Center";
        }
    }

    private void CancelBatchNightRun_Click(object sender, RoutedEventArgs e)
    {
        if (_batchCts == null || _batchCts.IsCancellationRequested) return;

        var confirm = _dialogs.ShowMessage(
            "Batch-Nachtbetrieb wirklich abbrechen?\n\nDie aktuelle Haltung wird noch fertig verarbeitet.",
            "Abbrechen?", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _batchCts.Cancel();
        BtnBatchCancel.IsEnabled = false;
        BtnBatchCancel.Content = "Wird abgebrochen...";
        Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Abbruch angefordert — aktuelle Haltung wird fertig verarbeitet...\n");
    }

    private static bool TryCreateKnowledgeBaseSnapshot(out string info)
    {
        try
        {
            var dbPath = Ai.KnowledgeRoot.GetKnowledgeDbPath();
            if (!File.Exists(dbPath))
            {
                info = "KnowledgeBase.db nicht gefunden.";
                return false;
            }

            var rootDir = System.IO.Path.GetDirectoryName(dbPath) ?? Ai.KnowledgeRoot.GetRoot();
            var snapshotDir = System.IO.Path.Combine(rootDir, "snapshots");
            Directory.CreateDirectory(snapshotDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetDbPath = System.IO.Path.Combine(snapshotDir, $"KnowledgeBase_{stamp}.db");
            File.Copy(dbPath, targetDbPath, overwrite: true);

            var walPath = dbPath + "-wal";
            if (File.Exists(walPath))
                File.Copy(walPath, targetDbPath + "-wal", overwrite: true);

            var shmPath = dbPath + "-shm";
            if (File.Exists(shmPath))
                File.Copy(shmPath, targetDbPath + "-shm", overwrite: true);

            info = targetDbPath;
            return true;
        }
        catch (Exception ex)
        {
            info = ex.Message;
            return false;
        }
    }
}
