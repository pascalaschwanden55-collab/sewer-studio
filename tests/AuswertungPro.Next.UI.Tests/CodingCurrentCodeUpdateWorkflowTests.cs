using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCurrentCodeUpdateWorkflowTests
{
    [Fact]
    public void Execute_hides_badge_without_view_model()
    {
        var calls = new List<string>();

        var result = CodingCurrentCodeUpdateWorkflow.Execute(
            new CodingCurrentCodeUpdateRequest(HasViewModel: false),
            Actions(
                calls,
                getEvents: () => throw new InvalidOperationException("Events should not be read."),
                resolveCurrentMeter: () => throw new InvalidOperationException("Meter should not be resolved.")));

        Assert.Equal(CodingCurrentCodeUpdateOutcome.Hidden, result.Outcome);
        Assert.Equal(["apply:False:"], calls);
    }

    [Fact]
    public void Execute_builds_current_code_state_when_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingCurrentCodeUpdateWorkflow.Execute(
            new CodingCurrentCodeUpdateRequest(HasViewModel: true),
            Actions(calls));

        Assert.Equal(CodingCurrentCodeUpdateOutcome.Applied, result.Outcome);
        Assert.Equal(
            [
                "events",
                "meter",
                "apply:True:1.20m BBA Riss"
            ],
            calls);
    }

    private static CodingCurrentCodeUpdateActions Actions(
        List<string> calls,
        Func<IEnumerable<CodingEvent>>? getEvents = null,
        Func<double>? resolveCurrentMeter = null)
        => new(
            GetEvents: getEvents ?? (() =>
            {
                calls.Add("events");
                return [Event(1.2, "BBA", "Riss")];
            }),
            ResolveCurrentMeter: resolveCurrentMeter ?? (() =>
            {
                calls.Add("meter");
                return 1.3;
            }),
            ApplyState: state => calls.Add($"apply:{state.IsVisible}:{state.Text}"));

    private static CodingEvent Event(double meter, string code, string description)
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
