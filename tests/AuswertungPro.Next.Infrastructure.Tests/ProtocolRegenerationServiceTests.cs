using System;
using System.IO;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProtocolRegenerationServiceTests
{
    [Fact]
    public void RegenerateOne_writes_E_protocol_into_haltung_folder_and_links_pdf_eigen()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "sewertest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var project = new Project();
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "TEST-1", FieldSource.Manual, userEdited: false);
            record.Protocol = new ProtocolDocument
            {
                HaltungId = "TEST-1",
                Current = new ProtocolRevision
                {
                    Entries = { new ProtocolEntry { Code = "BAB", MeterStart = 1.0, Beschreibung = "Riss" } }
                }
            };
            project.Data.Add(record);

            var dest = ProtocolRegenerationService.RegenerateOne(project, tempRoot, record, record.Protocol);

            Assert.False(string.IsNullOrWhiteSpace(dest));
            Assert.True(File.Exists(dest!));
            Assert.EndsWith("_E.pdf", dest);
            Assert.Contains("Haltungen_Verteilt", dest);
            Assert.Contains("TEST-1", dest);

            var pdfEigen = record.GetFieldValue("PDF_Eigen");
            Assert.False(string.IsNullOrWhiteSpace(pdfEigen));
            Assert.Contains("Haltungen_Verteilt", pdfEigen);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* Aufraeumen best-effort */ }
        }
    }

    [Fact]
    public void RegenerateOne_uses_date_prefix_from_distributed_original_pdf_when_record_date_is_empty()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "sewertest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var project = new Project();
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "10081-8993", FieldSource.Manual, userEdited: false);
            record.SetFieldValue("PDF_Path", "Haltungen_Verteilt/10081-8993/20260622_10081-8993.pdf", FieldSource.Manual, userEdited: false);
            record.Protocol = new ProtocolDocument
            {
                HaltungId = "10081-8993",
                Current = new ProtocolRevision
                {
                    Entries = { new ProtocolEntry { Code = "BAB", MeterStart = 1.0, Beschreibung = "Riss" } }
                }
            };
            project.Data.Add(record);

            var dest = ProtocolRegenerationService.RegenerateOne(project, tempRoot, record, record.Protocol);

            Assert.False(string.IsNullOrWhiteSpace(dest));
            Assert.Equal("20260622_10081-8993_E.pdf", Path.GetFileName(dest!));
            Assert.Equal(
                "Haltungen_Verteilt/10081-8993/20260622_10081-8993_E.pdf",
                record.GetFieldValue("PDF_Eigen")?.Replace('\\', '/'));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RegenerateOne_returns_null_when_no_haltungsname()
    {
        var project = new Project();
        var record = new HaltungRecord(); // Haltungsname bleibt leer
        var doc = new ProtocolDocument();

        var dest = ProtocolRegenerationService.RegenerateOne(project, Path.GetTempPath(), record, doc);

        Assert.Null(dest);
    }
}
