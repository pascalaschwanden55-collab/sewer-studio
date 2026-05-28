using System;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

public sealed record CodingFeedbackDecision(
    string CaseId,
    string SuggestedCode,
    string FinalCode,
    bool Accepted,
    string Label,
    double MeterStart,
    double MeterEnd,
    string Severity,
    double Confidence,
    string? Reason,
    string? PositionClock,
    int? HeightMm,
    int? WidthMm,
    int? CrossSectionReductionPercent);

public static class CodingFeedbackDecisionMapper
{
    public static CodingFeedbackDecision? TryCreate(CodingEvent ev, string caseId)
    {
        if (ev is null) throw new ArgumentNullException(nameof(ev));
        if (ev.AiContext is null || ev.AiContext.Decision == CodingUserDecision.Ignored)
            return null;

        var suggestedCode = FirstNonEmpty(ev.AiContext.SuggestedCode, ev.Entry.Ai?.SuggestedCode);
        if (string.IsNullOrWhiteSpace(suggestedCode))
            return null;

        var accepted = ev.AiContext.Decision is CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit;
        var finalCode = accepted ? ev.Entry.Code ?? "" : "";
        var meterStart = ev.Entry.MeterStart ?? ev.MeterAtCapture;
        var meterEnd = ev.Entry.MeterEnd ?? meterStart;
        var label = FirstNonEmpty(ev.Entry.Beschreibung, caseId, suggestedCode) ?? suggestedCode;

        return new CodingFeedbackDecision(
            CaseId: caseId,
            SuggestedCode: suggestedCode,
            FinalCode: finalCode,
            Accepted: accepted,
            Label: label,
            MeterStart: meterStart,
            MeterEnd: meterEnd,
            Severity: ev.Entry.CodeMeta?.Severity ?? "",
            Confidence: ev.AiContext.Confidence,
            Reason: ev.AiContext.Reason,
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
