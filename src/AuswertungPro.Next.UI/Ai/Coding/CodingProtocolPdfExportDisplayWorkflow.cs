using System;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingProtocolPdfExportDisplayRequest(
    HaltungRecord Record,
    ProtocolDocument Document,
    string? LastProjectPath,
    ProtocolPdfExporter Exporter);

public sealed record CodingProtocolPdfExportDisplayActions(
    Func<ProtocolPdfExporter, CodingProtocolPdfExportService> CreateService);

internal sealed record CodingProtocolPdfExportDisplayRequestCore(
    HaltungRecord Record,
    ProtocolDocument Document,
    string? LastProjectPath,
    IProtocolPdfExporter Exporter);

internal sealed record CodingProtocolPdfExportDisplayActionsCore(
    Func<IProtocolPdfExporter, CodingProtocolPdfExportService> CreateService);

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
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);
        return OfferCore(
            request.Record,
            request.Document,
            request.LastProjectPath,
            request.Exporter,
            exporter => actions.CreateService((ProtocolPdfExporter)exporter));
    }

    internal static bool Offer(CodingProtocolPdfExportDisplayRequestCore request)
        => Offer(
            request,
            new CodingProtocolPdfExportDisplayActionsCore(
                CodingProtocolPdfExportServiceFactory.Create));

    internal static bool Offer(
        CodingProtocolPdfExportDisplayRequestCore request,
        CodingProtocolPdfExportDisplayActionsCore actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);
        return OfferCore(
            request.Record,
            request.Document,
            request.LastProjectPath,
            request.Exporter,
            actions.CreateService);
    }

    private static bool OfferCore(
        HaltungRecord? record,
        ProtocolDocument? document,
        string? lastProjectPath,
        IProtocolPdfExporter? exporter,
        Func<IProtocolPdfExporter, CodingProtocolPdfExportService> createService)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(createService);

        return CodingProtocolPdfExportOfferWorkflow.Offer(
            record,
            document,
            lastProjectPath,
            new CodingProtocolPdfExportOfferWorkflowActions(
                CreateService: () => createService(exporter)));
    }
}
