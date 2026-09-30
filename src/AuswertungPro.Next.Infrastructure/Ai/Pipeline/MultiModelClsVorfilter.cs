using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// YOLO-cls-Vorfilter der Mehrmodell-Analyse (Frame-Quality-Gate und Bildklassifikator vor der
/// Detektion), ausgelagert aus <see cref="MultiModelAnalysisService"/> (AP05b). Gilt bewusst AUCH fuer
/// Sweep-/BCD-/BCE-Bilder: vorher konnten schwarze oder strukturlose Bilder ungefiltert bis zu Qwen
/// (120-s-Grenze) laufen. Eigene Regeln, bewusst nicht mit YOLO/DINO/SAM gleichgesetzt:
/// Der Vorfilter ist kein Pflichtschritt. Faellt er aus - auch wegen VRAM-Mangels oder eines
/// Transportfehlers -, laeuft das Bild normal weiter (kein Fehler, keine Skip-Quote, keine
/// Ausfallserie). Nur ein Nutzerabbruch wird weitergereicht. Ein vom Vorfilter verworfenes Bild ist
/// regulaer uebersprungen.
/// </summary>
internal sealed class MultiModelClsVorfilter
{
    private readonly IVisionPipelineClient _client;
    private readonly ILogger _logger;

    public MultiModelClsVorfilter(IVisionPipelineClient client, ILogger logger)
    {
        _client = client;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ist <see cref="Abschluss"/> gesetzt, endet das Bild hier (regulaer uebersprungen). Sonst geht
    /// <see cref="Antwort"/> als Klassifikator-Signal weiter; null, wenn der Vorfilter ausfiel.
    /// </summary>
    internal sealed record Ergebnis(MultiModelBildErgebnis? Abschluss, YoloClassifyResponse? Antwort);

    public async Task<Ergebnis> PruefeAsync(
        MultiModelLaufZustand run, MultiModelBildKontext bild, bool classifierDecisionEnabled, CancellationToken ct)
    {
        var trace = bild.Trace;
        YoloClassifyResponse? clsResult = null;
        try
        {
            clsResult = await _client.ClassifyYoloAsync(
                new YoloClassifyRequest(bild.FrameBase64, 3), ct).ConfigureAwait(false);

            if (ClsPrefilterRule.Decide(clsResult, classifierDecisionEnabled) is { } skip)
            {
                run.SkippedFrames++;
                if (skip.EmptyPrediction is { } empty)
                {
                    run.CodeVoting.RegisterAndVote(null, bild.EstimatedMeter);   // Fenster altern lassen
                    trace.ClassifierCode = "LEER";
                    trace.ClassifierConfidence = empty.Confidence;
                    trace.ClassifierModel = MultiModelAnalysisService.ClassifierModelTag(clsResult);
                }
                _logger.LogDebug("Frame {Frame}: cls-Vorfilter {Reason} → skip", run.FrameIndex, skip.ProgressText);
                run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                    $"Frame {run.FrameIndex}/{run.TotalFrames} – {skip.ProgressText} → skip"));
                bild.RecordFrame(run, 0, 0, 0);
                trace.Path = skip.TracePath;
                trace.YoloRelevant = false;
                trace.DropReason = skip.DropReason;
                return new Ergebnis(MultiModelBildErgebnis.Uebersprungen(bild.EstimatedMeter), clsResult);
            }

            var topPred = clsResult.Predictions.Count > 0 ? clsResult.Predictions[0] : null;

            if (topPred != null)
                _logger.LogDebug("Frame {Frame}: YOLO-cls '{Class}' ({Conf:F0}%) → weiter zur Detektion",
                    run.FrameIndex, topPred.ClassName, topPred.Confidence * 100);
            if (classifierDecisionEnabled && !clsResult.ClassifierLoaded)
            {
                MultiModelAnalysisService.MarkTraceDegraded(trace, "classifier_not_loaded");
                _logger.LogWarning("Frame {Frame}: YOLO-cls Modell nicht geladen - Klassifikator-Code wird nicht angewendet.",
                    run.FrameIndex);
            }

            if (classifierDecisionEnabled && clsResult.BendVetoFailed)
            {
                MultiModelAnalysisService.MarkTraceDegraded(trace, "bend_veto_failed");
                _logger.LogWarning("Frame {Frame}: Bogen-Veto fehlgeschlagen - is_bend=false wird nicht fuer Klassifikator-Code vertraut.",
                    run.FrameIndex);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzerabbruch: sofort weiterwerfen, nie als Fehler zaehlen.
            throw;
        }
        catch (Exception ex)
        {
            // cls-Modell nicht verfuegbar → normal weiter (kein harter Fehler)
            if (classifierDecisionEnabled)
                _logger.LogWarning(ex, "Frame {Frame}: YOLO-cls im Klassifikator-Entscheidungsmodus nicht verfuegbar; falle auf Detektionspfad zurueck", run.FrameIndex);
            else
                _logger.LogDebug(ex, "Frame {Frame}: YOLO-cls nicht verfuegbar, ueberspringe Vorfilter", run.FrameIndex);
        }

        return new Ergebnis(null, clsResult);
    }
}
