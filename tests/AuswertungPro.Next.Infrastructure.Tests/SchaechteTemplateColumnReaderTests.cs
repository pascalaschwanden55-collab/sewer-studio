using System;
using System.IO;
using ClosedXML.Excel;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchaechteTemplateColumnReaderTests
{
    [Fact]
    public void Only_export_excel_reader_exists()
    {
        var duplicateImportReader = Path.Combine(
            RepoRoot(),
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Import",
            "Xlsx",
            "SchaechteTemplateColumnReader.cs");

        Assert.False(
            File.Exists(duplicateImportReader),
            "SchaechteTemplateColumnReader darf nur unter Infrastructure/Export/Excel existieren, sonst gibt es zwei konkurrierende Wahrheiten.");
    }

    [Fact]
    public void LoadFromExportDirectory_prefers_exact_template_and_keeps_unique_trimmed_headers()
    {
        var root = CreateTempRoot();
        var exportDir = Directory.CreateDirectory(Path.Combine(root, "Export_Vorlage")).FullName;
        var exactPath = Path.Combine(exportDir, "Schaechte.xlsx");

        WriteWorkbook(
            exactPath,
            "Schaechte",
            [" Funktion ", "Schachtnummer", "", "Daten", "Daten"]);
        WriteWorkbook(
            Path.Combine(exportDir, "Fallback-Schaechte.xlsx"),
            "Schaechte",
            ["Ignored"]);

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(root);

        Assert.True(result.TemplateFound);
        Assert.Equal(exactPath, result.TemplatePath);
        Assert.Equal(["Schachtnummer", "Funktion", "Daten"], result.Columns);
    }

    [Fact]
    public void LoadFromExportDirectory_uses_matching_fallback_when_exact_template_is_missing()
    {
        var root = CreateTempRoot();
        var exportDir = Directory.CreateDirectory(Path.Combine(root, "Export_Vorlage")).FullName;
        var fallbackPath = Path.Combine(exportDir, "Meine-Schaechte-Vorlage.xlsx");

        WriteWorkbook(
            fallbackPath,
            "Andere",
            ["Schachtnummer", "Funktion"]);

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(root);

        Assert.True(result.TemplateFound);
        Assert.Equal(fallbackPath, result.TemplatePath);
        Assert.Equal(["Funktion", "Schachtnummer"], result.Columns);
    }

    [Fact]
    public void LoadFromExportDirectory_returns_empty_result_when_directory_is_missing()
    {
        var root = CreateTempRoot();

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(root);

        Assert.False(result.TemplateFound);
        Assert.Equal(string.Empty, result.TemplatePath);
        Assert.Empty(result.Columns);
    }

    private static string CreateTempRoot()
        => Path.Combine(Path.GetTempPath(), $"schaechte-columns-{Guid.NewGuid():N}");

    private static void WriteWorkbook(string path, string worksheetName, string[] headers)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(worksheetName);
        const int headerRow = 12;

        for (var i = 0; i < headers.Length; i++)
            worksheet.Cell(headerRow, i + 1).Value = headers[i];

        workbook.SaveAs(path);
    }
}
