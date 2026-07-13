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

    [Fact]
    public void Interner_Player_Pfad_akzeptiert_den_Schnittstellen_Exporter()
    {
        IProtocolPdfExporter exporter = new ThrowingProtocolPdfExporter();
        IProtocolPdfExporter? received = null;
        var document = new ProtocolDocument();
        document.Current.Entries.Add(new ProtocolEntry { Code = "BAJ" });
        var service = new CodingProtocolPdfExportService(
            confirmPdfExport: _ => false,
            buildPlan: (_, _, _, _) => throw new InvalidOperationException(),
            chooseOutputPath: _ => throw new InvalidOperationException(),
            getCurrentProject: () => throw new InvalidOperationException(),
            buildPdf: (_, _, _, _, _) => throw new InvalidOperationException(),
            saveAndOpen: (_, _) => throw new InvalidOperationException(),
            showPdfExportFailed: _ => throw new InvalidOperationException(),
            now: () => throw new InvalidOperationException(),
            baseDirectory: () => throw new InvalidOperationException());

        var exported = CodingProtocolPdfExportDisplayWorkflow.Offer(
            new CodingProtocolPdfExportDisplayRequestCore(
                new HaltungRecord(),
                document,
                LastProjectPath: null,
                exporter),
            new CodingProtocolPdfExportDisplayActionsCore(actual =>
            {
                received = actual;
                return service;
            }));

        Assert.False(exported);
        Assert.Same(exporter, received);
    }

    private sealed class ThrowingProtocolPdfExporter : IProtocolPdfExporter
    {
        public byte[] BuildPdf(string projectTitle, ProtocolDocument document, string projectRootAbs)
            => throw new NotSupportedException();

        public byte[] BuildPdf(
            string projectTitle,
            ProtocolDocument document,
            string projectRootAbs,
            ProtocolPdfExportOptions options)
            => throw new NotSupportedException();

        public byte[] BuildHaltungsprotokollPdf(
            Project project,
            HaltungRecord record,
            ProtocolDocument document,
            string projectRootAbs,
            HaltungsprotokollPdfOptions? options = null)
            => throw new NotSupportedException();

        public byte[] BuildCsv(ProtocolDocument document, ProtocolPdfExportOptions? options = null)
            => throw new NotSupportedException();
    }
}
