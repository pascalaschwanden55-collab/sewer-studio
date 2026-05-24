using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.QualityGate;

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
        if (ev.AiContext is null || ev.AiContext.Decision == CodingUserDecision.Ignored)
            return;

        var suggestedCode = FirstNonEmpty(ev.AiContext.SuggestedCode, ev.Entry.Ai?.SuggestedCode);
        if (string.IsNullOrWhiteSpace(suggestedCode))
            return;

        var accepted = ev.AiContext.Decision is CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit;
        var finalCode = accepted ? ev.Entry.Code ?? "" : "";

        var mapped = new MappedProtocolEntry(
            Detection: ToDetection(ev, caseId, suggestedCode),
            SuggestedCode: suggestedCode,
            Confidence: ev.AiContext.Confidence,
            Reason: ev.AiContext.Reason,
            Warnings: Array.Empty<string>());

        using var db = _contextFactory();
        var logger = new ValidationLogger(db.Connection);
        var weights = new WeightLearningService(db.Connection);
        var feedback = new FeedbackIngestionService(logger, weights);

        await feedback.ProcessFeedbackAsync(mapped, finalCode, accepted, ct).ConfigureAwait(false);
    }

    private static RawVideoDetection ToDetection(CodingEvent ev, string caseId, string suggestedCode)
    {
        var meterStart = ev.Entry.MeterStart ?? ev.MeterAtCapture;
        var meterEnd = ev.Entry.MeterEnd ?? meterStart;
        var label = FirstNonEmpty(ev.Entry.Beschreibung, caseId, suggestedCode) ?? suggestedCode;

        return new RawVideoDetection(
            FindingLabel: label,
            MeterStart: meterStart,
            MeterEnd: meterEnd,
            Severity: ev.Entry.CodeMeta?.Severity ?? "",
            VsaCodeHint: suggestedCode,
            PositionClock: FormatClock(ev.Overlay?.ClockFrom),
            HeightMm: ToNullableInt(ev.Overlay?.Q1Mm),
            WidthMm: ToNullableInt(ev.Overlay?.Q2Mm),
            CrossSectionReductionPercent: ToNullableInt(ev.Overlay?.FillPercent));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static string? FormatClock(double? clock)
        => clock.HasValue
            ? clock.Value.ToString("0.#", CultureInfo.InvariantCulture)
            : null;

    private static int? ToNullableInt(double? value)
        => value.HasValue
            ? (int)Math.Round(value.Value, MidpointRounding.AwayFromZero)
            : null;
}
