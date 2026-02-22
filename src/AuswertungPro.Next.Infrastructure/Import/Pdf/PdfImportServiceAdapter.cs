using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed class PdfImportServiceAdapter : IPdfImportService
{
    private readonly LegacyPdfImportService _svc = new();

    public Result<ImportStats> ImportPdf(string pdfPath, Project project, string? pdfToTextPath)
    {
        try
        {
            var stats = _svc.ImportPdf(pdfPath, project, pdfToTextPath);

            // Legacy ImportStats -> new ImportStats
            var msg = stats.Messages.Select(m => $"{m.Level}: {m.Message} {m.Context}".Trim()).ToList();
            var mapped = new ImportStats(
                Found: stats.Found,
                Created: stats.CreatedRecords,
                Updated: stats.UpdatedRecords,
                Errors: stats.Errors,
                Uncertain: stats.Uncertain,
                Messages: msg
            );
            return Result<ImportStats>.Success(mapped);
        }
        catch (Exception ex)
        {
            return Result<ImportStats>.Fail("IMP-PDF", ex.Message);
        }
    }
}
