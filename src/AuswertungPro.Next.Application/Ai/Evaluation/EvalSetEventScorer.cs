using System.Text;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public enum EvalSetEventFrameOutcome
{
    NotCorrectlyDetected = 0,
    CorrectlyDetectedGateBlocked = 1,
    CorrectlyDetectedGatePassed = 2
}

public sealed record EvalSetDamageEventFrameResult(
    string FrameId,
    string HoldingKey,
    string EventId,
    int ExpectedSeverity,
    EvalSetEventFrameOutcome Outcome);

public sealed record EvalSetEventMissStatistics(
    int EventCount,
    int Misses,
    double MissRate,
    double WilsonLower95,
    double WilsonUpper95,
    double ExactOneSidedUpper95);

public sealed record EvalSetEventOutcomeSummary(
    int EventCount,
    int DetectedEvents,
    int GatePassedEvents,
    EvalSetEventMissStatistics DetectionMisses,
    EvalSetEventMissStatistics GateMisses);

public sealed record EvalSetEventScore(
    EvalSetEventOutcomeSummary AllEvents,
    EvalSetEventOutcomeSummary SeverityFourOrFiveEvents,
    int RequiredSeverityFourOrFiveEvents,
    bool HasMinimumSeverityFourOrFiveEvents);

public static class EvalSetEventScorer
{
    public const int RequiredSevereEventCount = 20;

    public static EvalSetEventScore Score(IReadOnlyList<EvalSetDamageEventFrameResult> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var events = new Dictionary<EventKey, EventAggregate>();
        foreach (var frame in frames)
        {
            ValidateFrame(frame);

            var eventId = frame.EventId.Trim();
            var holdingKey = frame.HoldingKey.Trim();
            var eventKey = EventKey.Create(holdingKey, eventId);
            if (!events.TryGetValue(eventKey, out var aggregate))
            {
                aggregate = new EventAggregate(frame.ExpectedSeverity);
                events.Add(eventKey, aggregate);
            }
            else if (aggregate.ExpectedSeverity != frame.ExpectedSeverity)
            {
                throw new ArgumentException(
                    $"Die Metadaten fuer Haltung '{holdingKey}', Ereignis '{eventId}' sind nicht konsistent.",
                    nameof(frames));
            }

            aggregate.Detected |= frame.Outcome != EvalSetEventFrameOutcome.NotCorrectlyDetected;
            aggregate.GatePassed |= frame.Outcome == EvalSetEventFrameOutcome.CorrectlyDetectedGatePassed;
        }

        var allEvents = events.Values.ToList();
        var severeEvents = allEvents
            .Where(item => item.ExpectedSeverity >= 4)
            .ToList();

        return new EvalSetEventScore(
            BuildSummary(allEvents),
            BuildSummary(severeEvents),
            RequiredSevereEventCount,
            severeEvents.Count >= RequiredSevereEventCount);
    }

    private static EvalSetEventOutcomeSummary BuildSummary(IReadOnlyList<EventAggregate> events)
    {
        var detected = events.Count(item => item.Detected);
        var gatePassed = events.Count(item => item.GatePassed);
        var detectionMisses = events.Count - detected;
        var gateMisses = events.Count - gatePassed;

        return new EvalSetEventOutcomeSummary(
            events.Count,
            detected,
            gatePassed,
            ToPublicEstimate(BinomialStatistics.EstimateRate95(events.Count, detectionMisses)),
            ToPublicEstimate(BinomialStatistics.EstimateRate95(events.Count, gateMisses)));
    }

    private static EvalSetEventMissStatistics ToPublicEstimate(BinomialRateEstimate estimate)
        => new(
            estimate.Trials,
            estimate.Occurrences,
            estimate.Rate,
            estimate.WilsonLower95,
            estimate.WilsonUpper95,
            estimate.ExactUpper95);

    private static void ValidateFrame(EvalSetDamageEventFrameResult frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ValidateNormalizedId(frame.FrameId, nameof(frame.FrameId));
        ValidateNormalizedId(frame.HoldingKey, nameof(frame.HoldingKey));
        ValidateNormalizedId(frame.EventId, nameof(frame.EventId));

        if (frame.ExpectedSeverity is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                frame.ExpectedSeverity,
                "Severity muss zwischen 1 und 5 liegen.");
        }
        if (!Enum.IsDefined(frame.Outcome))
            throw new ArgumentOutOfRangeException(nameof(frame), frame.Outcome, "Unbekannter Frame-Ausgang.");
    }

    private static void ValidateNormalizedId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Die Kennung fehlt.", parameterName);
        if (!value.Equals(value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Die Kennung darf keine Rand-Leerzeichen enthalten.", parameterName);
    }

    private readonly record struct EventKey(string HoldingKey, string EventId)
    {
        public static EventKey Create(string holdingKey, string eventId)
            => new(
                (EvalContaminationGuard.NormalizeHaltungKey(holdingKey) ?? holdingKey)
                    .Normalize(NormalizationForm.FormC)
                    .ToUpperInvariant(),
                eventId.Normalize(NormalizationForm.FormC).ToUpperInvariant());
    }

    private sealed class EventAggregate(int expectedSeverity)
    {
        public int ExpectedSeverity { get; } = expectedSeverity;
        public bool Detected { get; set; }
        public bool GatePassed { get; set; }
    }
}
