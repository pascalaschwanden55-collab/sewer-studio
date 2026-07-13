using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

/// <summary>
/// Processes Accept/Reject feedback:
/// 1. Logs to ValidationLog
/// 2. On Accept: indexes the corrected sample in the KB — but ONLY if it carries a real
///    frame/holding identity (Audit Fix #6b: a sample without FramePath and without a real
///    haltung CaseId is invisible to the eval-contamination guard and a near-empty embedding,
///    so it is deliberately skipped).
/// 3. On Reject: currently only recorded in ValidationLog. NOTE: no hard-negative learning is
///    implemented yet (was previously claimed here but never built).
/// 4. Every 25 validations: triggers WeightLearningService.ReLearnAsync()
/// </summary>
public sealed class FeedbackIngestionService
{
    public int ReLearnInterval { get; set; } = 25;

    private readonly ValidationLogger _logger;
    private readonly WeightLearningService _weightLearner;
    private readonly ITrainingSampleIndexer? _sampleIndexer;
    private int _feedbackCount;

    public FeedbackIngestionService(
        ValidationLogger logger,
        WeightLearningService weightLearner,
        ITrainingSampleIndexer? sampleIndexer = null)
    {
        _logger = logger;
        _weightLearner = weightLearner;
        _sampleIndexer = sampleIndexer;
    }

    /// <summary>Process user feedback for a detection.</summary>
    public async Task ProcessFeedbackAsync(
        MappedProtocolEntry entry,
        string finalCode,
        bool accepted,
        CancellationToken ct = default)
    {
        var suggestedCode = entry.SuggestedCode ?? "";
        // Die Gewichte bewerten die Zuverlaessigkeit des vorgeschlagenen Codes.
        // Eine Korrektur darf deshalb nicht unter dem neuen Zielcode gruppiert werden.
        var vsaCode = suggestedCode;
        var wasCorrect = accepted && string.Equals(suggestedCode, finalCode, StringComparison.OrdinalIgnoreCase);

        _logger.Log(vsaCode, suggestedCode, finalCode, wasCorrect, entry.Detection.Evidence);

        if (accepted && _sampleIndexer is not null && !string.IsNullOrWhiteSpace(finalCode))
        {
            var det = entry.Detection;
            var sample = new TrainingSample
            {
                SampleId = $"feedback_{Guid.NewGuid():N}",
                CaseId = det.FindingLabel ?? "",
                Code = finalCode,
                Beschreibung = det.FindingLabel ?? "",
                MeterStart = det.MeterStart,
                MeterEnd = det.MeterEnd,
                Status = TrainingSampleStatus.Approved,
                HumanConfirmed = true,
                Corrected = !string.Equals(suggestedCode, finalCode, StringComparison.OrdinalIgnoreCase),
                ConfirmedAtUtc = DateTime.UtcNow
            };

            // Audit Fix #6b: Ein Feedback-Sample aus RawVideoDetection traegt KEINEN FramePath
            // und keine echte Haltungs-CaseId (CaseId = Befund-Label). Damit ist es (a) fuer den
            // Eval-Kontaminationsschutz unsichtbar (weder Frame-Hash noch Haltungs-Sperrliste
            // koennen greifen) und (b) ein nahezu inhaltsleeres Embedding. Solche Samples NICHT
            // indexieren, bis ein echter Frame-/Haltungsbezug durchgereicht wird.
            var hasFrame = !string.IsNullOrWhiteSpace(sample.FramePath);
            var hasHaltungId = !string.IsNullOrWhiteSpace(sample.CaseId)
                && System.Text.RegularExpressions.Regex.IsMatch(sample.CaseId, @"\d[\d.]*[-/]\d[\d.]*");

            if (!hasFrame && !hasHaltungId)
            {
                BestEffort.ReportWarning(
                    $"[FeedbackIngestion] Sample {sample.SampleId} NICHT indexiert: kein Frame-/Haltungsbezug " +
                    "(Eval-Guard waere blind, Embedding inhaltsleer).");
            }
            else
            {
                try
                {
                    await _sampleIndexer.IndexSampleAsync(sample, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Frueher stilles catch{} — jetzt sichtbar (Audit): KB-Indexierung nicht kritisch,
                    // der Fehler darf aber nicht spurlos verschwinden.
                    BestEffort.ReportWarning(
                        $"[FeedbackIngestion] KB-Indexierung fehlgeschlagen: {ex.Message}");
                }
            }
        }

        _feedbackCount++;

        // ReLearn-Trigger auf PERSISTENTEM Zaehler (Audit P0-1): Der fruehere Instanz-Zaehler
        // _feedbackCount begann bei jedem neuen FeedbackIngestionService wieder bei 0 — und der
        // CodingFeedbackRecorder legt pro Benutzerentscheidung einen neuen Service an. Dadurch
        // erreichte er die Schwelle von 25 nie. Die Zahl der ValidationLog-Zeilen ueberlebt
        // Service- und App-Neustarts und zaehlt zuverlaessig weiter.
        var persistentCount = _logger.GetTotalCount();
        if (persistentCount > 0 && persistentCount % ReLearnInterval == 0)
        {
            try
            {
                await _weightLearner.ReLearnAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Weight learning failure is non-critical.
                BestEffort.ReportWarning(
                    $"[FeedbackIngestion] Weight-Learning fehlgeschlagen: {ex.Message}");
            }
        }
    }

    /// <summary>Feedback-Ereignisse, die diese Service-Instanz verarbeitet hat (nicht persistent).</summary>
    public int TotalProcessed => _feedbackCount;
}
