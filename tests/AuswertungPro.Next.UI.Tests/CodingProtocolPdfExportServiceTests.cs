using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolPdfExportServiceTests
{
    [Fact]
    public void TryOfferPdfExport_builds_and_saves_pdf_after_confirmation()
    {
        var record = new HaltungRecord();
        var doc = BuildProtocolDocument(entryCount: 2);
        var project = new Project();
        var saved = new List<(string Path, byte[] Bytes)>();
        var failures = new List<string>();
        var service = new CodingProtocolPdfExportService(
            confirmPdfExport: count => count == 2,
            buildPlan: (haltung, lastProjectPath, baseDirectory, now) =>
            {
                Assert.Same(record, haltung);
                Assert.Equal(@"C:\project\project.json", lastProjectPath);
                Assert.Equal(@"C:\app", baseDirectory);
                Assert.Equal(new DateTime(2026, 6, 23), now);
                return new CodingProtocolPdfExportPlan(
                    "default.pdf",
                    @"C:\project",
                    new HaltungsprotokollPdfOptions { IncludePhotos = true });
            },
            chooseOutputPath: defaultFileName =>
            {
                Assert.Equal("default.pdf", defaultFileName);
                return @"C:\out.pdf";
            },
            getCurrentProject: () => project,
            buildPdf: (actualProject, actualRecord, actualDoc, projectRoot, options) =>
            {
                Assert.Same(project, actualProject);
                Assert.Same(record, actualRecord);
                Assert.Same(doc, actualDoc);
                Assert.Equal(@"C:\project", projectRoot);
                Assert.True(options.IncludePhotos);
                return [1, 2, 3];
            },
            saveAndOpen: (path, pdf) => saved.Add((path, pdf)),
            showPdfExportFailed: failures.Add,
            now: () => new DateTime(2026, 6, 23),
            baseDirectory: () => @"C:\app");

        var exported = service.TryOfferPdfExport(record, doc, @"C:\project\project.json");

        Assert.True(exported);
        var write = Assert.Single(saved);
        Assert.Equal(@"C:\out.pdf", write.Path);
        Assert.Equal([1, 2, 3], write.Bytes);
        Assert.Empty(failures);
    }

    [Fact]
    public void TryOfferPdfExport_returns_false_without_work_when_confirmation_is_declined()
    {
        var service = new CodingProtocolPdfExportService(
            confirmPdfExport: _ => false,
            buildPlan: (_, _, _, _) => throw new InvalidOperationException("Plan must not be built."),
            chooseOutputPath: _ => throw new InvalidOperationException("Dialog must not open."),
            getCurrentProject: () => throw new InvalidOperationException("Project must not be read."),
            buildPdf: (_, _, _, _, _) => throw new InvalidOperationException("PDF must not be built."),
            saveAndOpen: (_, _) => throw new InvalidOperationException("PDF must not be saved."),
            showPdfExportFailed: _ => throw new InvalidOperationException("Failure dialog must not show."),
            now: () => throw new InvalidOperationException("Clock must not be read."),
            baseDirectory: () => throw new InvalidOperationException("Base directory must not be read."));

        var exported = service.TryOfferPdfExport(new HaltungRecord(), BuildProtocolDocument(entryCount: 1), "");

        Assert.False(exported);
    }

    [Fact]
    public void TryOfferPdfExport_reports_failure_and_returns_false_when_export_throws()
    {
        var failures = new List<string>();
        var service = new CodingProtocolPdfExportService(
            confirmPdfExport: _ => true,
            buildPlan: (_, _, _, _) => new CodingProtocolPdfExportPlan(
                "default.pdf",
                "",
                new HaltungsprotokollPdfOptions()),
            chooseOutputPath: _ => "out.pdf",
            getCurrentProject: () => new Project(),
            buildPdf: (_, _, _, _, _) => throw new InvalidOperationException("boom"),
            saveAndOpen: (_, _) => throw new InvalidOperationException("Save must not be reached."),
            showPdfExportFailed: failures.Add,
            now: () => DateTime.UnixEpoch,
            baseDirectory: () => "");

        var exported = service.TryOfferPdfExport(new HaltungRecord(), BuildProtocolDocument(entryCount: 1), "");

        Assert.False(exported);
        Assert.Equal(["boom"], failures);
    }

    [Fact]
    public void Factory_creates_export_service()
    {
        var service = CodingProtocolPdfExportServiceFactory.Create(new ProtocolPdfExporter());

        Assert.NotNull(service);
    }

    private static ProtocolDocument BuildProtocolDocument(int entryCount)
    {
        var doc = new ProtocolDocument();
        for (var i = 0; i < entryCount; i++)
        {
            doc.Current.Entries.Add(new ProtocolEntry { Code = $"C{i}" });
        }

        return doc;
    }
}
