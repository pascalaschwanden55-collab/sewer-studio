using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using System.Threading;

namespace AuswertungPro.Next.Application.Export;

public interface IExcelExportService
{
    Result ExportToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow);

    Result ExportToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath)
        => ExportToTemplate(project, templatePath, outputPath, headerRow, startRow);

    Result ExportToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ExportToTemplate(
            project, templatePath, outputPath, headerRow, startRow, projectFilePath);
    }

    Result ExportSchaechteToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow);

    Result ExportSchaechteToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath)
        => ExportSchaechteToTemplate(project, templatePath, outputPath, headerRow, startRow);

    Result ExportSchaechteToTemplate(
        Project project,
        string templatePath,
        string outputPath,
        int headerRow,
        int startRow,
        string? projectFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ExportSchaechteToTemplate(
            project, templatePath, outputPath, headerRow, startRow, projectFilePath);
    }
}
