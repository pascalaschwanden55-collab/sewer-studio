using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingProtocolPreviewCommandWorkflowOutcome
{
    NoRecord,
    NoServiceProvider,
    NotOpened,
    Opened
}

public sealed record CodingProtocolPreviewCommandRequest(
    bool HasHaltungRecord,
    bool HasLegacyServiceProvider,
    ProtocolDocument Document);

public sealed record CodingProtocolPreviewCommandActions(
    Func<bool> ShowPreview,
    Func<ProtocolDocument?> GetCurrentProtocol,
    Action<ProtocolDocument> SyncPrimaryDamages,
    Action<ProtocolDocument> OfferPdfExport);

public sealed record CodingProtocolPreviewCommandWorkflowResult(
    CodingProtocolPreviewCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingProtocolPreviewCommandWorkflowOutcome.Opened;
}

public static class CodingProtocolPreviewCommandWorkflow
{
    public static CodingProtocolPreviewCommandWorkflowResult Execute(
        CodingProtocolPreviewCommandRequest request,
        CodingProtocolPreviewCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasHaltungRecord)
            return Result(CodingProtocolPreviewCommandWorkflowOutcome.NoRecord);

        if (!request.HasLegacyServiceProvider)
            return Result(CodingProtocolPreviewCommandWorkflowOutcome.NoServiceProvider);

        if (!actions.ShowPreview())
            return Result(CodingProtocolPreviewCommandWorkflowOutcome.NotOpened);

        var currentProtocol = actions.GetCurrentProtocol();
        if (currentProtocol is not null)
            actions.SyncPrimaryDamages(currentProtocol);

        actions.OfferPdfExport(currentProtocol ?? request.Document);
        return Result(CodingProtocolPreviewCommandWorkflowOutcome.Opened);
    }

    private static CodingProtocolPreviewCommandWorkflowResult Result(
        CodingProtocolPreviewCommandWorkflowOutcome outcome)
        => new(outcome);
}
