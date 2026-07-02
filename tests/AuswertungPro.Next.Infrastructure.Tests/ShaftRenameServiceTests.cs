using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ShaftRenameServiceTests
{
    [Fact]
    public void Rename_NeueStruktur_RelativerPdfPath_BenenntVerteiltesPdfUm()
    {
        var oldNumber = "22152";
        var newNumber = "99999";
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldNumber);
        var newSan = ProjectPathResolver.SanitizePathSegment(newNumber);

        var root = Path.Combine(Path.GetTempPath(), $"shaftrename-{Guid.NewGuid():N}");
        var projectFile = Path.Combine(root, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        var oldFolder = Path.Combine(root, "Sch\u00e4chte_Verteilt", oldSan);
        Directory.CreateDirectory(oldFolder);
        File.WriteAllText(Path.Combine(oldFolder, $"20260618_{oldSan}.pdf"), "pdf");

        try
        {
            var record = new SchachtRecord();
            record.SetFieldValue("Schachtnummer", oldNumber);
            record.SetFieldValue("PDF_Path", Path.Combine("Sch\u00e4chte_Verteilt", oldSan, $"20260618_{oldSan}.pdf"));

            var result = ShaftRenameService.Rename(record, oldNumber, newNumber, projectFile);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.FolderRenamed);

            var newFolder = Path.Combine(root, "Sch\u00e4chte_Verteilt", newSan);
            Assert.True(Directory.Exists(newFolder), "Schacht-Ordner wurde nicht umbenannt.");
            Assert.False(Directory.Exists(oldFolder), "Alter Schacht-Ordner existiert noch.");
            Assert.True(File.Exists(Path.Combine(newFolder, $"20260618_{newSan}.pdf")), "PDF-Datei wurde nicht umbenannt.");

            var pdfPath = record.GetFieldValue("PDF_Path");
            Assert.Contains(newSan, pdfPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(oldSan, pdfPath, StringComparison.OrdinalIgnoreCase);
            Assert.False(Path.IsPathRooted(pdfPath), $"PDF_Path soll relativ bleiben: {pdfPath}");

            var resolved = ProjectPathResolver.ResolveFilePath(pdfPath, projectFile);
            Assert.False(string.IsNullOrWhiteSpace(resolved), "PDF_Path ist nicht gegen den Projektroot aufloesbar.");
            Assert.True(File.Exists(resolved!), $"PDF_Path zeigt auf keine existierende Datei: {resolved}");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Rename_PdfPfadMitAbweichendemOrdnernamen_BenenntTatsaechlichenOrdnerUndPdfUm()
    {
        var oldNumber = "547";
        var existingFolderName = "547.01";
        var existingPdfName = "20260618_457.pdf";
        var newNumber = "547.02";
        var newSan = ProjectPathResolver.SanitizePathSegment(newNumber);

        var root = Path.Combine(Path.GetTempPath(), $"shaftrename-{Guid.NewGuid():N}");
        var projectFile = Path.Combine(root, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        var oldFolder = Path.Combine(root, "Sch\u00e4chte_Verteilt", existingFolderName);
        Directory.CreateDirectory(oldFolder);
        File.WriteAllText(Path.Combine(oldFolder, existingPdfName), "pdf");

        try
        {
            var record = new SchachtRecord();
            record.SetFieldValue("Schachtnummer", oldNumber);
            record.SetFieldValue("PDF_Path", Path.Combine("Sch\u00e4chte_Verteilt", existingFolderName, existingPdfName));

            var result = ShaftRenameService.Rename(record, oldNumber, newNumber, projectFile);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.FolderRenamed);

            var newFolder = Path.Combine(root, "Sch\u00e4chte_Verteilt", newSan);
            var newPdf = Path.Combine(newFolder, $"20260618_{newSan}.pdf");
            Assert.True(Directory.Exists(newFolder), "Tatsaechlicher Schacht-Ordner wurde nicht umbenannt.");
            Assert.False(Directory.Exists(oldFolder), "Alter tatsaechlicher Schacht-Ordner existiert noch.");
            Assert.True(File.Exists(newPdf), "PDF-Datei wurde nicht auf die neue Schachtnummer umbenannt.");

            var pdfPath = record.GetFieldValue("PDF_Path");
            Assert.Contains(newSan, pdfPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(existingFolderName, pdfPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("457", pdfPath, StringComparison.OrdinalIgnoreCase);

            var resolved = ProjectPathResolver.ResolveFilePath(pdfPath, projectFile);
            Assert.Equal(newPdf, resolved);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
