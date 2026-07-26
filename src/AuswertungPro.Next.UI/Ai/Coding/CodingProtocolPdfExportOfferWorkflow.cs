using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingProtocolPdfExportOfferWorkflowActions(
    Func<CodingProtocolPdfExportService> CreateService);

public static class CodingProtocolPdfExportOfferWorkflow
{
    public static bool Offer(
        HaltungRecord record,
        ProtocolDocument document,
        string? lastProjectPath,
        CodingProtocolPdfExportOfferWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        return service.TryOfferPdfExport(record, document, lastProjectPath);
    }
}
