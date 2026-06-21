using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    /// <summary>
    /// Exportiert Approved-Samples im YOLO-Format über den Sidecar.
    /// Erzeugt images/, labels/ und data.yaml für YOLO-Training.
    /// </summary>
    [RelayCommand]
    private async Task ExportYoloAsync()
    {
        if (IsBusy) return;

        var candidates = Samples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && !string.IsNullOrWhiteSpace(s.FramePath)
                        && File.Exists(s.FramePath))
            .ToList();
        var approved = candidates
            .Where(IsTrainingExportEligible)
            .ToList();

        if (candidates.Count != approved.Count)
            await PersistSamplesAsync();

        if (approved.Count == 0)
        {
            StatusText = "Keine Approved-Samples mit gültigen Frames vorhanden.";
            Log("YOLO-Export: Keine exportierbaren Samples gefunden.");
            return;
        }

        // Zielordner wählen
        var dlg = new OpenFolderDialog { Title = "YOLO-Export Zielordner wählen" };
        if (dlg.ShowDialog() != true)
            return;

        var outputDir = dlg.FolderName;

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        try
        {
            IsBusy = true;
            Log($"YOLO-Export: {approved.Count} Samples → {outputDir}");
            StatusText = $"YOLO-Export: {approved.Count} Samples werden vorbereitet...";

            // Sidecar-Verbindung prüfen
            var pipelineCfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToPipelineConfig();
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarToken: pipelineCfg.SidecarToken);

            var health = await client.HealthCheckAsync(ct).ConfigureAwait(false);
            if (health is null)
            {
                // Fallback: lokaler Export ohne Sidecar
                Log($"Sidecar nicht erreichbar ({pipelineCfg.SidecarUrl}). Versuche lokalen Export...");
                await ExportYoloLocalAsync(approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            Log($"Sidecar erreichbar: v{health.Version}, GPU: {health.Gpu?.CurrentModel ?? "?"}");

            // Samples zu DTOs konvertieren
            ProgressMax = approved.Count;
            ProgressValue = 0;

            // Eval-Guard: kein eingefrorenes Eval-Bild darf in den Trainings-Export (Audit R4)
            var sidecarEvalRoot = AppSettings.Load().EvalSetRoot;
            var sidecarEvalHashes = EvalContaminationGuard.LoadEvalImageHashes(sidecarEvalRoot);
            var sidecarEvalHaltungen = EvalContaminationGuard.LoadEvalHaltungKeys(sidecarEvalRoot);
            int skipEvalHash = 0, skipEvalCase = 0, skipNoBox = 0;

            var exportSamples = new List<TrainingExportSample>();
            for (var i = 0; i < approved.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var s = approved[i];
                ProgressValue = i + 1;
                StatusText = $"YOLO-Export: Lade Frame {i + 1}/{approved.Count}...";

                switch (EvalContaminationGuard.ClassifyForExport(sidecarEvalHashes, sidecarEvalHaltungen, s.FramePath, s.CaseId))
                {
                    case EvalContaminationGuard.ExportContaminationResult.EvalImageHash: skipEvalHash++; continue;
                    case EvalContaminationGuard.ExportContaminationResult.EvalHaltung: skipEvalCase++; continue;
                }

                // YOLO nur mit ECHTER Box — keine Dummy-BBox mehr (Audit R4)
                if (string.IsNullOrWhiteSpace(s.Code) || !s.HasBbox) { skipNoBox++; continue; }

                var bytes = await File.ReadAllBytesAsync(s.FramePath, ct).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(bytes);

                var labels = new List<TrainingExportSampleLabel>
                {
                    new(s.Code, s.BboxXCenter!.Value, s.BboxYCenter!.Value, s.BboxWidth!.Value, s.BboxHeight!.Value)
                };
                exportSamples.Add(new TrainingExportSample(base64, labels));
            }

            if (skipEvalHash + skipEvalCase + skipNoBox > 0)
                Log($"  uebersprungen: {skipEvalHash} Eval-Hash, {skipEvalCase} Eval-Haltung, {skipNoBox} ohne echte Box");

            if (exportSamples.Count == 0)
            {
                Log("YOLO-Export: nach Eval-/Box-Filter keine Samples uebrig.");
                StatusText = "YOLO-Export: keine exportierbaren Samples (Eval/Box-Filter).";
                return;
            }

            StatusText = $"YOLO-Export: Sende {exportSamples.Count} Samples an Sidecar...";
            var request = new TrainingExportRequestDto(exportSamples, outputDir, 0.8);
            TrainingExportResponseDto response;
            try
            {
                response = await client.ExportTrainingAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Sidecar-Export nicht moeglich ({ex.Message}). Lokaler Export wird verwendet...");
                await ExportYoloLocalAsync(approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            // Samples als exportiert markieren
            foreach (var s in approved)
                s.ExportedUtc = DateTime.UtcNow;
            await PersistSamplesAsync();

            var msg = $"YOLO-Export fertig: {response.TotalSamples} Samples " +
                      $"({response.TrainCount} Train, {response.ValCount} Val), " +
                      $"{response.ClassesUsed.Count} Klassen → {outputDir}";
            Log(msg);
            Log($"  data.yaml: {response.DataYamlPath}");
            Log($"  Klassen: {string.Join(", ", response.ClassesUsed)}");
            StatusText = msg;
        }
        catch (OperationCanceledException)
        {
            Log("YOLO-Export abgebrochen.");
            StatusText = "YOLO-Export abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"YOLO-Export FEHLER: {ex.Message}");
            StatusText = $"YOLO-Export fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Lokaler YOLO-Export — bevorzugt TeacherAnnotations (echte BBoxen),
    /// Fallback auf TrainingSamples (Dummy-BBoxen nur wenn keine Annotationen vorhanden).
    /// </summary>
    private async Task ExportYoloLocalAsync(
        List<TrainingSample> approved, string outputDir, CancellationToken ct)
    {
        // TeacherAnnotations laden (echte BBoxen)
        var annotations = await TeacherAnnotationStore.LoadAsync();
        var annotationsWithImages = annotations
            .Where(a => !string.IsNullOrWhiteSpace(a.FullFramePath) && File.Exists(a.FullFramePath))
            .ToList();

        Log($"YOLO-Export: {annotationsWithImages.Count} TeacherAnnotations mit Bildern, {approved.Count} TrainingSamples");

        // Eval-Guard: kein eingefrorenes Eval-Bild in den Export (Hash + Haltung). (Audit R4)
        var localEvalRoot = AppSettings.Load().EvalSetRoot;
        var localEvalHashes = EvalContaminationGuard.LoadEvalImageHashes(localEvalRoot);
        var localEvalHaltungen = EvalContaminationGuard.LoadEvalHaltungKeys(localEvalRoot);
        int locSkipEvalHash = 0, locSkipEvalCase = 0;

        var imgTrain = Path.Combine(outputDir, "images", "train");
        var imgVal = Path.Combine(outputDir, "images", "val");
        var lblTrain = Path.Combine(outputDir, "labels", "train");
        var lblVal = Path.Combine(outputDir, "labels", "val");
        foreach (var d in new[] { imgTrain, imgVal, lblTrain, lblVal })
            Directory.CreateDirectory(d);

        int totalExported = 0;

        // ── Phase 1: TeacherAnnotations exportieren (echte BBoxen) ──
        if (annotationsWithImages.Count > 0)
        {
            var splitIdx = (int)(annotationsWithImages.Count * 0.8);
            ProgressMax = annotationsWithImages.Count;

            for (var i = 0; i < annotationsWithImages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var a = annotationsWithImages[i];
                ProgressValue = i + 1;
                StatusText = $"YOLO-Export (Teacher): {i + 1}/{annotationsWithImages.Count}...";

                switch (EvalContaminationGuard.ClassifyForExport(localEvalHashes, localEvalHaltungen, a.FullFramePath, a.HaltungName))
                {
                    case EvalContaminationGuard.ExportContaminationResult.EvalImageHash: locSkipEvalHash++; continue;
                    case EvalContaminationGuard.ExportContaminationResult.EvalHaltung: locSkipEvalCase++; continue;
                }

                var isTrain = i < splitIdx;
                var imgDir = isTrain ? imgTrain : imgVal;
                var lblDir = isTrain ? lblTrain : lblVal;

                // Bild kopieren
                var ext = Path.GetExtension(a.FullFramePath);
                var imgDst = Path.Combine(imgDir, $"teacher_{a.AnnotationId}{ext}");
                File.Copy(a.FullFramePath!, imgDst, overwrite: true);

                // Label mit echten BBoxen schreiben
                var clsIdx = VsaYoloClassMap.GetClassId(a.VsaCode);
                var bbox = a.BoundingBox;
                var lblPath = Path.Combine(lblDir, $"teacher_{a.AnnotationId}.txt");
                if (bbox is not null && bbox.Width > 0 && bbox.Height > 0)
                {
                    // Echte BBox aus TeacherAnnotation
                    await File.WriteAllTextAsync(lblPath,
                        $"{clsIdx} {bbox.XCenter:F6} {bbox.YCenter:F6} {bbox.Width:F6} {bbox.Height:F6}", ct);
                }
                else
                {
                    // Annotation ohne BBox → Vollbild als Fallback
                    await File.WriteAllTextAsync(lblPath,
                        $"{clsIdx} 0.500000 0.500000 1.000000 1.000000", ct);
                }

                totalExported++;
            }
        }

        // ── Phase 2: TrainingSamples IMMER exportieren (mit echten BBoxen wenn vorhanden) ──
        if (approved.Count > 0)
        {
            int withBbox = approved.Count(s => s.HasBbox);
            Log($"  Exportiere {withBbox} TrainingSamples mit echter Box (von {approved.Count}; {approved.Count - withBbox} ohne Box uebersprungen)");
            var sampleSplitIdx = (int)(approved.Count * 0.8);

            for (var i = 0; i < approved.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var s = approved[i];
                StatusText = $"YOLO-Export (Samples): {i + 1}/{approved.Count}...";

                var isTrain = i < sampleSplitIdx;
                var imgDir = isTrain ? imgTrain : imgVal;
                var lblDir = isTrain ? lblTrain : lblVal;

                // Sicherheitscheck: Frame-Datei koennte zwischen Filter und Export geloescht worden sein
                if (!File.Exists(s.FramePath)) continue;
                if (!s.HasBbox) continue;   // YOLO nur mit echter Box — keine Dummy-Labels, kein Bild ohne Label

                switch (EvalContaminationGuard.ClassifyForExport(localEvalHashes, localEvalHaltungen, s.FramePath, s.CaseId))
                {
                    case EvalContaminationGuard.ExportContaminationResult.EvalImageHash: locSkipEvalHash++; continue;
                    case EvalContaminationGuard.ExportContaminationResult.EvalHaltung: locSkipEvalCase++; continue;
                }

                var ext = Path.GetExtension(s.FramePath);
                var imgDst = Path.Combine(imgDir, $"sample_{i:D6}{ext}");
                try { File.Copy(s.FramePath, imgDst, overwrite: true); }
                catch (IOException) { continue; } // Datei gesperrt oder nicht mehr vorhanden

                var clsIdx = VsaYoloClassMap.GetClassId(s.Code);
                var lblPath = Path.Combine(lblDir, $"sample_{i:D6}.txt");

                // Echte BBox aus Eingabemarker
                await File.WriteAllTextAsync(lblPath,
                    $"{clsIdx} {s.BboxXCenter!.Value:F6} {s.BboxYCenter!.Value:F6} " +
                    $"{s.BboxWidth!.Value:F6} {s.BboxHeight!.Value:F6}", ct);

                s.ExportedUtc = DateTime.UtcNow;
                totalExported++;
            }
            await PersistSamplesAsync();
        }

        // ── data.yaml mit exaktem Klassenmapping ──
        var fullMap = VsaYoloClassMap.GetFullMap();
        var sortedClasses = fullMap.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

        var yamlPath = Path.Combine(outputDir, "data.yaml");
        var yamlLines = new[]
        {
            $"path: {Path.GetFullPath(outputDir)}",
            "train: images/train",
            "val: images/val",
            $"nc: {sortedClasses.Count}",
            $"names: [{string.Join(", ", sortedClasses.Select(c => $"'{c}'"))}]"
        };
        await File.WriteAllLinesAsync(yamlPath, yamlLines, ct);

        // classes.txt exportieren
        await VsaYoloClassMap.ExportClassesTxtAsync(
            Path.Combine(outputDir, "classes.txt"));

        if (locSkipEvalHash + locSkipEvalCase > 0)
            Log($"  Eval-Schutz: {locSkipEvalHash} per Hash, {locSkipEvalCase} per Haltung uebersprungen.");

        var msg = $"YOLO-Export fertig: {totalExported} Samples " +
                  $"({annotationsWithImages.Count} Teacher + {totalExported - annotationsWithImages.Count} Samples), " +
                  $"{sortedClasses.Count} Klassen → {outputDir}";
        Log(msg);
        Log($"  data.yaml: {yamlPath}");
        Log($"  Klassen: {string.Join(", ", sortedClasses)}");
        StatusText = msg;
    }

    private bool IsTrainingExportEligible(TrainingSample sample)
    {
        var result = _codeCatalog is null
            ? TrainingSampleEligibility.Evaluate(sample)
            : TrainingSampleEligibility.Evaluate(sample, _codeCatalog);
        sample.TrainingEligible = result.IsEligible;
        sample.TrainingEligibilityReason = result.Reason;
        return result.IsEligible;
    }
}
