using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCurrentCodeBadgePolicyTests
{
    [Fact]
    public void Build_hides_badge_without_events()
    {
        var state = CodingCurrentCodeBadgePolicy.Build(Array.Empty<CodingEvent>(), currentMeter: 1.0);

        Assert.False(state.IsVisible);
        Assert.Equal("", state.Text);
    }

    [Fact]
    public void Build_shows_nearest_event_inside_half_meter()
    {
        var state = CodingCurrentCodeBadgePolicy.Build(
            new[] { Event(1.42, "BBA", "Riss"), Event(3.0, "BCA", "Loch") },
            currentMeter: 1.5);

        Assert.True(state.IsVisible);
        Assert.Equal("1.42m BBA Riss", state.Text);
    }

    [Fact]
    public void Build_prefers_closest_nearby_event()
    {
        var state = CodingCurrentCodeBadgePolicy.Build(
            new[] { Event(1.1, "BBA"), Event(1.4, "BCC") },
            currentMeter: 1.5);

        Assert.Equal("1.40m BCC", state.Text);
    }

    [Fact]
    public void Build_shows_next_event_when_none_is_nearby()
    {
        var state = CodingCurrentCodeBadgePolicy.Build(
            new[] { Event(4.2, "BCA"), Event(2.0, "BBA") },
            currentMeter: 1.0);

        Assert.True(state.IsVisible);
        Assert.Equal("in 1.0m: BBA", state.Text);
    }

    [Fact]
    public void Build_hides_badge_after_last_event()
    {
        var state = CodingCurrentCodeBadgePolicy.Build(
            new[] { Event(1.0, "BBA") },
            currentMeter: 3.0);

        Assert.False(state.IsVisible);
    }

    [Fact]
    public void Build_uses_placeholder_for_missing_code()
    {
        var state = CodingCurrentCodeBadgePolicy.Build(
            new[] { Event(1.0, "") },
            currentMeter: 1.1);

        Assert.Equal("1.00m ???", state.Text);
    }

    private static CodingEvent Event(double meter, string code, string description = "")
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = description
            }
        };
}
