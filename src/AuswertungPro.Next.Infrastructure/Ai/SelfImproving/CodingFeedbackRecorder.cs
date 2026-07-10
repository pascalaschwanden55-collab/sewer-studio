using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

/// <summary>
/// Bruecke vom interaktiven Coding-Modus in den persistenten Feedback-/Lernpfad.
/// Die Serviceinstanz darf weiterhin kurzlebig sein; Schwelle und Lernfortschritt liegen in SQLite.
/// </summary>
public sealed class CodingFeedbackRecorder : ICodingFeedbackRecorder
{
    private readonly Func<KnowledgeBaseContext> _contextFactory;
    private readonly ITrainingSampleIndexer? _sampleIndexer;

    public CodingFeedbackRecorder()
        : this(() => new KnowledgeBaseContext(), sampleIndexer: null)
    {
    }

    public CodingFeedbackRecorder(ITrainingSampleIndexer sampleIndexer)
        : this(() => new KnowledgeBaseContext(), sampleIndexer)
    {
    }

    public CodingFeedbackRecorder(string dbPath, ITrainingSampleIndexer? sampleIndexer = null)
        : this(() => new KnowledgeBaseContext(dbPath), sampleIndexer)
    {
    }

    internal CodingFeedbackRecorder(
        Func<KnowledgeBaseContext> contextFactory,
        ITrainingSampleIndexer? sampleIndexer = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _sampleIndexer = sampleIndexer;
    }

    public async Task RecordDecisionAsync(CodingEvent ev, string caseId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ev);
        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, caseId);
        if (decision is null)
            return;

        var mapped = new MappedProtocolEntry(
            Detection: ToDetection(decision, ev.AiContext),
            SuggestedCode: decision.SuggestedCode,
            Confidence: decision.Confidence,
            Reason: decision.Reason,
            Warnings: Array.Empty<string>());

        TrainingSample? confirmedSample = null;
        if (decision.Accepted)
        {
            var photos = ev.Entry.FotoPaths;
            confirmedSample = CodingEventToSampleMapper.FromCodingEvent(
                ev,
                caseId,
                framePath: photos.FirstOrDefault(),
                confirmedAtUtc: DateTime.UtcNow,
                evidenceFramePath: photos.Skip(1).FirstOrDefault());
        }

        using var db = _contextFactory();
        var logger = new ValidationLogger(db.Connection);
        var weights = new WeightLearningService(db.Connection);
        var feedback = new FeedbackIngestionService(logger, weights, _sampleIndexer);

        await feedback.ProcessFeedbackAsync(
            mapped,
            decision.FinalCode,
            decision.Accepted,
            confirmedSample,
            ct).ConfigureAwait(false);
    }

    private static RawVideoDetection ToDetection(
        CodingFeedbackDecision decision,
        CodingEventAiContext? aiContext)
    {
        var evidence = aiContext is null
            ? null
            : new EvidenceVector(
                LlmCodeConf: aiContext.Confidence,
                KbCodeAgreement: aiContext.KbCodeAgreement,
                PlausibilityScore: aiContext.QualityGateLevel is not null ? aiContext.Confidence : null,
                DamageCategory: decision.FinalCode);

        return new RawVideoDetection(
            FindingLabel: decision.Label,
            MeterStart: decision.MeterStart,
            MeterEnd: decision.MeterEnd,
            Severity: decision.Severity,
            VsaCodeHint: decision.SuggestedCode,
            PositionClock: decision.PositionClock,
            HeightMm: decision.HeightMm,
            WidthMm: decision.WidthMm,
            CrossSectionReductionPercent: decision.CrossSectionReductionPercent,
            Evidence: evidence);
    }
}
