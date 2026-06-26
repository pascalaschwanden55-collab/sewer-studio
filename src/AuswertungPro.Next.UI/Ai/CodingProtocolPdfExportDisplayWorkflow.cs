using System;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingProtocolPdfExportDisplayRequest(
    HaltungRecord Record,
    ProtocolDocument Document,
    string? LastProjectPath,
    ProtocolPdfExporter Exporter);

public sealed record CodingProtocolPdfExportDisplayActions(
    Func<ProtocolPdfExporter, CodingProtocolPdfExportService> CreateService);

public static class CodingProtocolPdfExportDisplayWorkflow
{
    public static bool Offer(CodingProtocolPdfExportDisplayRequest request)
        => Offer(
            request,
            new CodingProtocolPdfExportDisplayActions(
                CodingProtocolPdfExportServiceFactory.Create));

    public static bool Offer(
        CodingProtocolPdfExportDisplayRequest request,
        CodingProtocolPdfExportDisplayActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Record);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Exporter);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        return CodingProtocolPdfExportOfferWorkflow.Offer(
            request.Record,
            request.Document,
            request.LastProjectPath,
            new CodingProtocolPdfExportOfferWorkflowActions(
                CreateService: () => actions.CreateService(request.Exporter)));
    }
}
