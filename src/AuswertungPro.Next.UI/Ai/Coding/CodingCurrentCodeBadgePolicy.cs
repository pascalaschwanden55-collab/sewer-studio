using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingCurrentCodeBadgeState(bool IsVisible, string Text)
{
    public static CodingCurrentCodeBadgeState Hidden { get; } = new(false, string.Empty);
}

public static class CodingCurrentCodeBadgePolicy
{
    private const double NearbyToleranceMeters = 0.5;

    public static CodingCurrentCodeBadgeState Build(
        IEnumerable<CodingEvent> events,
        double currentMeter)
    {
        ArgumentNullException.ThrowIfNull(events);

        var orderedEvents = events.ToList();
        if (orderedEvents.Count == 0)
            return CodingCurrentCodeBadgeState.Hidden;

        var nearestEvent = orderedEvents
            .Where(ev => Math.Abs(ev.MeterAtCapture - currentMeter) < NearbyToleranceMeters)
            .OrderBy(ev => Math.Abs(ev.MeterAtCapture - currentMeter))
            .FirstOrDefault();

        if (nearestEvent is not null)
        {
            var code = string.IsNullOrWhiteSpace(nearestEvent.Entry.Code)
                ? "???"
                : nearestEvent.Entry.Code;
            var description = string.IsNullOrWhiteSpace(nearestEvent.Entry.Beschreibung)
                ? ""
                : $" {nearestEvent.Entry.Beschreibung}";

            return new CodingCurrentCodeBadgeState(
                true,
                $"{nearestEvent.MeterAtCapture:F2}m {code}{description}");
        }

        var nextEvent = orderedEvents
            .Where(ev => ev.MeterAtCapture > currentMeter)
            .OrderBy(ev => ev.MeterAtCapture)
            .FirstOrDefault();

        if (nextEvent is null)
            return CodingCurrentCodeBadgeState.Hidden;

        var distanceMeters = nextEvent.MeterAtCapture - currentMeter;
        var nextCode = string.IsNullOrWhiteSpace(nextEvent.Entry.Code)
            ? "???"
            : nextEvent.Entry.Code;

        return new CodingCurrentCodeBadgeState(
            true,
            $"in {distanceMeters:F1}m: {nextCode}");
    }
}
