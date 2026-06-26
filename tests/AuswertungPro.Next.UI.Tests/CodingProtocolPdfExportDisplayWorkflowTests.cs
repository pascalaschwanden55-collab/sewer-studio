using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfExportDisplayWorkflowTests
{
    [Fact]
    public void Offer_creates_service_from_exporter_and_delegates_to_offer_workflow()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var document = new ProtocolDocument();
        document.Current.Entries.Add(new ProtocolEntry { Code = "BAJ" });
        var exporter = new ProtocolPdfExporter();
        var service = new CodingProtocolPdfExportService(
            confirmPdfExport: count =>
            {
                calls.Add($"confirm:{count}");
                return false;
            },
            buildPlan: (_, _, _, _) => throw new InvalidOperationException("Plan must not be built."),
            chooseOutputPath: _ => throw new InvalidOperationException("Dialog must not open."),
            getCurrentProject: () => throw new InvalidOperationException("Project must not be read."),
            buildPdf: (_, _, _, _, _) => throw new InvalidOperationException("PDF must not be built."),
            saveAndOpen: (_, _) => throw new InvalidOperationException("PDF must not be saved."),
            showPdfExportFailed: _ => throw new InvalidOperationException("Failure dialog must not show."),
            now: () => throw new InvalidOperationException("Clock must not be read."),
            baseDirectory: () => throw new InvalidOperationException("Base directory must not be read."));

        var exported = CodingProtocolPdfExportDisplayWorkflow.Offer(
            new CodingProtocolPdfExportDisplayRequest(
                record,
                document,
                LastProjectPath: @"C:\project\project.json",
                exporter),
            new CodingProtocolPdfExportDisplayActions(
                CreateService: actualExporter =>
                {
                    calls.Add("service");
                    Assert.Same(exporter, actualExporter);
                    return service;
                }));

        Assert.False(exported);
        Assert.Equal(["service", "confirm:1"], calls);
    }
}
