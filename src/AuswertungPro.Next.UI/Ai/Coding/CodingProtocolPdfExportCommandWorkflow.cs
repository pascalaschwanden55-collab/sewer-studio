using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingProtocolPdfExportCommandWorkflowOutcome
{
    NoExporter,
    NoRecord,
    NotExported,
    Exported
}

public sealed record CodingProtocolPdfExportCommandRequest(
    bool HasProtocolPdfExporter,
    bool HasHaltungRecord,
    ProtocolDocument Document);

public sealed record CodingProtocolPdfExportCommandActions(
    Func<bool> OfferPdfExport,
    Action<string, TimeSpan> ShowOverlay);

public sealed record CodingProtocolPdfExportCommandWorkflowResult(
    CodingProtocolPdfExportCommandWorkflowOutcome Outcome)
{
    public bool Completed => Outcome == CodingProtocolPdfExportCommandWorkflowOutcome.Exported;
}

public static class CodingProtocolPdfExportCommandWorkflow
{
    public static CodingProtocolPdfExportCommandWorkflowResult Execute(
        CodingProtocolPdfExportCommandRequest request,
        CodingProtocolPdfExportCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasProtocolPdfExporter)
            return Result(CodingProtocolPdfExportCommandWorkflowOutcome.NoExporter);

        if (!request.HasHaltungRecord)
            return Result(CodingProtocolPdfExportCommandWorkflowOutcome.NoRecord);

        if (!actions.OfferPdfExport())
            return Result(CodingProtocolPdfExportCommandWorkflowOutcome.NotExported);

        actions.ShowOverlay("PDF-Protokoll erstellt", TimeSpan.FromSeconds(4));
        return Result(CodingProtocolPdfExportCommandWorkflowOutcome.Exported);
    }

    private static CodingProtocolPdfExportCommandWorkflowResult Result(
        CodingProtocolPdfExportCommandWorkflowOutcome outcome)
        => new(outcome);
}
