using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
// using AuswertungPro.Next.Infrastructure.Import.Common; // entfernt, um Namenskonflikt zu vermeiden

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

public sealed class XtfImportServiceAdapter : IXtfImportService
{
    private readonly LegacyXtfImportService _svc = new();

    public Result<AuswertungPro.Next.Application.Import.ImportStats> ImportXtfFiles(IEnumerable<string> xtfPaths, Project project)
    {
        try
        {
            var stats = _svc.ImportXtfFiles(xtfPaths, project);

            var msg = stats.Messages.Select(m => $"{m.Level}: {m.Message} {m.Context}".Trim()).ToList();
            var mapped = new AuswertungPro.Next.Application.Import.ImportStats(
                Found: stats.Found,
                Created: stats.CreatedRecords,
                Updated: stats.UpdatedRecords,
                Errors: stats.Errors,
                Uncertain: stats.Uncertain,
                Messages: msg
            );
            return Result<AuswertungPro.Next.Application.Import.ImportStats>.Success(mapped);
        }
        catch (Exception ex)
        {
            return Result<AuswertungPro.Next.Application.Import.ImportStats>.Fail("IMP-XTF", ex.Message);
        }
    }
}
