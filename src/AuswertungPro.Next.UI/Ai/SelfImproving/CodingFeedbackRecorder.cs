using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Bruecke vom interaktiven Coding-Modus in den bestehenden FeedbackIngestionService.
/// Schreibt Accept/Reject/AcceptedWithEdit-Entscheidungen in ValidationLog.
/// </summary>
public sealed class CodingFeedbackRecorder : ICodingFeedbackRecorder
{
    private readonly Func<KnowledgeBaseContext> _contextFactory;

    public CodingFeedbackRecorder()
        : this(() => new KnowledgeBaseContext())
    {
    }

    public CodingFeedbackRecorder(string dbPath)
        : this(() => new KnowledgeBaseContext(dbPath))
    {
    }

    internal CodingFeedbackRecorder(Func<KnowledgeBaseContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task RecordDecisionAsync(CodingEvent ev, string caseId, CancellationToken ct = default)
    {
        if (ev is null) throw new ArgumentNullException(nameof(ev));
        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, caseId);
        if (decision is null)
            return;

        var mapped = new MappedProtocolEntry(
            Detection: ToDetection(decision),
            SuggestedCode: decision.SuggestedCode,
            Confidence: decision.Confidence,
            Reason: decision.Reason,
            Warnings: Array.Empty<string>());

        using var db = _contextFactory();
        var logger = new ValidationLogger(db.Connection);
        var weights = new WeightLearningService(db.Connection);
        var feedback = new FeedbackIngestionService(logger, weights);

        await feedback.ProcessFeedbackAsync(mapped, decision.FinalCode, decision.Accepted, ct).ConfigureAwait(false);
    }

    private static RawVideoDetection ToDetection(CodingFeedbackDecision decision)
        => new(
            FindingLabel: decision.Label,
            MeterStart: decision.MeterStart,
            MeterEnd: decision.MeterEnd,
            Severity: decision.Severity,
            VsaCodeHint: decision.SuggestedCode,
            PositionClock: decision.PositionClock,
            HeightMm: decision.HeightMm,
            WidthMm: decision.WidthMm,
            CrossSectionReductionPercent: decision.CrossSectionReductionPercent);
}
