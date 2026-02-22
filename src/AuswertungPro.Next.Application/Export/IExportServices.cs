using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Export;

public interface IExcelExportService
{
    Result ExportToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow);
    Result ExportSchaechteToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow);
}
