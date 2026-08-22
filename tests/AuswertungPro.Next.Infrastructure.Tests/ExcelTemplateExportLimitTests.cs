using AuswertungPro.Next.Infrastructure.Export.Excel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ExcelTemplateExportLimitTests
{
    [Fact]
    public void RejectIfExceeded_allows_safe_record_count()
    {
        var result = ExcelTemplateExportLimit.RejectIfExceeded(
            ExcelTemplateExportLimit.MaxRecords,
            "Haltungen",
            "EXP-EXCEL-LIMIT");

        Assert.Null(result);
    }

    [Fact]
    public void RejectIfExceeded_returns_clear_failure_above_limit()
    {
        var result = ExcelTemplateExportLimit.RejectIfExceeded(
            ExcelTemplateExportLimit.MaxRecords + 1,
            "Haltungen",
            "EXP-EXCEL-LIMIT");

        Assert.NotNull(result);
        Assert.False(result.Ok);
        Assert.Equal("EXP-EXCEL-LIMIT", result.ErrorCode);
        Assert.Contains(ExcelTemplateExportLimit.MaxRecords.ToString(), result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("aufteilen", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportToTemplate_rejects_oversized_project_before_loading_workbook()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "placeholder.xlsx");
        var outputPath = Path.Combine(directory.Path, "output.xlsx");
        File.WriteAllText(templatePath, "absichtlich keine echte Arbeitsmappe");
        var project = new Project();
        for (var index = 0; index <= ExcelTemplateExportLimit.MaxRecords; index++)
            project.Data.Add(new HaltungRecord());

        var result = new ExcelTemplateExportService().ExportToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow: 11,
            startRow: 12);

        Assert.False(result.Ok);
        Assert.Equal("EXP-EXCEL-LIMIT", result.ErrorCode);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void ExportSchaechteToTemplate_rejects_oversized_project_before_loading_workbook()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "placeholder.xlsx");
        var outputPath = Path.Combine(directory.Path, "output.xlsx");
        File.WriteAllText(templatePath, "absichtlich keine echte Arbeitsmappe");
        var project = new Project();
        for (var index = 0; index <= ExcelTemplateExportLimit.MaxRecords; index++)
            project.SchaechteData.Add(new SchachtRecord());

        var result = new ExcelTemplateExportService().ExportSchaechteToTemplate(
            project,
            templatePath,
            outputPath,
            headerRow: 11,
            startRow: 12);

        Assert.False(result.Ok);
        Assert.Equal("EXP-EXCEL-SCHACHT-LIMIT", result.ErrorCode);
        Assert.False(File.Exists(outputPath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
