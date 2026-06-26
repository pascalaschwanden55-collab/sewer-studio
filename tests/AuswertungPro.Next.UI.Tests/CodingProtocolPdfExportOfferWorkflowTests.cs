using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfExportOfferWorkflowTests
{
    [Fact]
    public void Offer_creates_export_service_and_delegates_request()
    {
        var calls = new List<string>();
        var record = new HaltungRecord();
        var document = new ProtocolDocument();
        document.Current.Entries.Add(new ProtocolEntry { Code = "BAJ" });
        var service = new CodingProtocolPdfExportService(
            confirmPdfExport: count =>
            {
                calls.Add($"confirm:{count}");
                return true;
            },
            buildPlan: (actualRecord, lastProjectPath, baseDirectory, now) =>
            {
                Assert.Same(record, actualRecord);
                Assert.Equal(@"C:\project\project.json", lastProjectPath);
                Assert.Equal(@"C:\app", baseDirectory);
                Assert.Equal(new DateTime(2026, 6, 25), now);
                calls.Add("plan");
                return new CodingProtocolPdfExportPlan(
                    "default.pdf",
                    @"C:\project",
                    new HaltungsprotokollPdfOptions());
            },
            chooseOutputPath: defaultFileName =>
            {
                calls.Add($"choose:{defaultFileName}");
                return null;
            },
            getCurrentProject: () => throw new InvalidOperationException("Project must not be read."),
            buildPdf: (_, _, _, _, _) => throw new InvalidOperationException("PDF must not be built."),
            saveAndOpen: (_, _) => throw new InvalidOperationException("PDF must not be saved."),
            showPdfExportFailed: _ => throw new InvalidOperationException("Failure dialog must not show."),
            now: () => new DateTime(2026, 6, 25),
            baseDirectory: () => @"C:\app");

        var exported = CodingProtocolPdfExportOfferWorkflow.Offer(
            record,
            document,
            @"C:\project\project.json",
            new CodingProtocolPdfExportOfferWorkflowActions(
                CreateService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.False(exported);
        Assert.Equal(["service", "confirm:1", "plan", "choose:default.pdf"], calls);
    }
}
