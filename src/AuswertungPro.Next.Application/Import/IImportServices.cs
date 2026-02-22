using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

public sealed record ImportStats(
    int Found,
    int Created,
    int Updated,
    int Errors,
    int Uncertain,
    IReadOnlyList<string> Messages
);

public interface IPdfImportService
{
    Result<ImportStats> ImportPdf(string pdfPath, Project project, string? pdfToTextPath);
}

public interface IXtfImportService
{
    Result<ImportStats> ImportXtfFiles(IEnumerable<string> xtfPaths, Project project);
}

public interface IWinCanDbImportService
{
    Result<ImportStats> ImportWinCanExport(string exportRoot, Project project);
}

public interface IIbakImportService
{
    Result<ImportStats> ImportIbakExport(string exportRoot, Project project);
}

public interface IKinsImportService
{
    Result<ImportStats> ImportKinsExport(string exportRoot, Project project);
}
