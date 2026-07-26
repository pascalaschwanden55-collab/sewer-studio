using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingImportConfirmCommandOutcome
{
    NoSelection,
    Rejected,
    Confirmed
}

public sealed record CodingImportConfirmCommandRequest(object? SelectedItem);

public sealed record CodingImportConfirmCommandActions(
    Func<CodingEvent, Task<bool>> ConfirmImportAsTrainingAsync);

public sealed record CodingImportConfirmCommandResult(
    CodingImportConfirmCommandOutcome Outcome)
{
    public bool Completed => Outcome == CodingImportConfirmCommandOutcome.Confirmed;
}

public static class CodingImportConfirmCommandWorkflow
{
    public static async Task<CodingImportConfirmCommandResult> ExecuteAsync(
        CodingImportConfirmCommandRequest request,
        CodingImportConfirmCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.SelectedItem is not CodingEvent importEvent)
            return Result(CodingImportConfirmCommandOutcome.NoSelection);

        var confirmed = await actions.ConfirmImportAsTrainingAsync(importEvent);
        return Result(
            confirmed
                ? CodingImportConfirmCommandOutcome.Confirmed
                : CodingImportConfirmCommandOutcome.Rejected);
    }

    private static CodingImportConfirmCommandResult Result(
        CodingImportConfirmCommandOutcome outcome)
        => new(outcome);
}
