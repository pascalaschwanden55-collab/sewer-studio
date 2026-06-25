using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingSelectCodeCommandOutcome
{
    NoViewModel,
    NoEntrySelected,
    Created
}

public sealed record CodingSelectCodeCommandRequest(
    bool HasViewModel);

public sealed record CodingSelectCodeCommandActions(
    Action PauseForCodingInteraction,
    Func<Func<Task>, Task> RunWithSuspendedOverlayInputAsync,
    Func<TimeSpan> GetCurrentVideoTime,
    Func<Task<double?>> ReadOsdMeterAsync,
    Func<double?, double?> ResolveManualEntryMeter,
    Func<TimeSpan, double?, ProtocolEntry?> CreateManualEntry,
    Func<ProtocolEntry, CodingEvent> AppendManualEvent,
    Action<CodingEvent> ApplyPostCreation);

public sealed record CodingSelectCodeCommandResult(
    CodingSelectCodeCommandOutcome Outcome);

public static class CodingSelectCodeCommandWorkflow
{
    public static async Task<CodingSelectCodeCommandResult> ExecuteAsync(
        CodingSelectCodeCommandRequest request,
        CodingSelectCodeCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasViewModel)
            return Result(CodingSelectCodeCommandOutcome.NoViewModel);

        var outcome = CodingSelectCodeCommandOutcome.NoEntrySelected;
        actions.PauseForCodingInteraction();

        await actions.RunWithSuspendedOverlayInputAsync(async () =>
        {
            var videoTime = actions.GetCurrentVideoTime();
            var osdMeter = await actions.ReadOsdMeterAsync();
            var meter = actions.ResolveManualEntryMeter(osdMeter);

            var entry = actions.CreateManualEntry(videoTime, meter);
            if (entry is null)
                return;

            var createdEvent = actions.AppendManualEvent(entry);
            actions.ApplyPostCreation(createdEvent);
            outcome = CodingSelectCodeCommandOutcome.Created;
        });

        return Result(outcome);
    }

    private static CodingSelectCodeCommandResult Result(CodingSelectCodeCommandOutcome outcome)
        => new(outcome);
}
