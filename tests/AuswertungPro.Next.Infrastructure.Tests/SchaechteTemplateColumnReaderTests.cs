using System;
using System.IO;
using ClosedXML.Excel;
using AuswertungPro.Next.Infrastructure.Export.Excel;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchaechteTemplateColumnReaderTests
{
    [Fact]
    public void InstanceReader_returns_empty_result_when_directory_is_missing()
    {
        ISchaechteTemplateColumnReader reader = new SchaechteTemplateColumnFileReader();

        var result = reader.LoadFromExportDirectory(CreateTempRoot());

        Assert.False(result.TemplateFound);
        Assert.Empty(result.Columns);
    }

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
        var exactPath = Path.Combine(exportDir, "Schächte.xlsx");

        WriteWorkbook(
            exactPath,
            "Schaechte",
            [" Funktion ", "Schachtnummer", "", "Daten", "0", "Daten"]);
        WriteWorkbook(
            Path.Combine(exportDir, "Schaechte.xlsx"),
            "Schaechte",
            ["Ignored"]);

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(root);

        Assert.True(result.TemplateFound);
        Assert.True(result.TemplateReadable);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(exactPath, result.TemplatePath);
        Assert.Equal(["Schachtnummer", "Funktion", "Daten"], result.Columns);
    }

    [Fact]
    public void ReadColumns_ignoriert_numerische_Platzhalter_Header()
    {
        var root = CreateTempRoot();
        var exportDir = Directory.CreateDirectory(Path.Combine(root, "Export_Vorlage")).FullName;
        var exactPath = Path.Combine(exportDir, "Schächte.xlsx");
        WriteWorkbook(
            exactPath,
            "Schaechte",
            ["Schachtnummer", "Abdeckung Stk.", "0", "Status"]);

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(root);

        Assert.Equal(["Schachtnummer", "Abdeckung Stk.", "Status"], result.Columns);
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

    [Fact]
    public void LoadFromExportDirectory_meldet_beschaedigte_Vorlage_ohne_Ausnahme()
    {
        var root = CreateTempRoot();
        var exportDir = Directory.CreateDirectory(Path.Combine(root, "Export_Vorlage")).FullName;
        File.WriteAllText(Path.Combine(exportDir, "Schächte.xlsx"), "keine Arbeitsmappe");

        var result = SchaechteTemplateColumnReader.LoadFromExportDirectory(root);

        Assert.False(result.TemplateFound);
        Assert.False(result.TemplateReadable);
        Assert.Empty(result.Columns);
        Assert.Contains("nicht lesbar", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempRoot()
        => Path.Combine(Path.GetTempPath(), $"schaechte-columns-{Guid.NewGuid():N}");

    private static void WriteWorkbook(string path, string worksheetName, string[] headers)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(worksheetName);
        // Nicht fest verdrahten: die Kopfzeile der Vorlage steht in
        // ExcelVorlagenLayout. Vorher stand hier eine 12, und als die Vorlage
        // ihre Kopfzeile verschob, las der Test an der falschen Stelle.
        var headerRow = AuswertungPro.Next.Application.Export.ExcelVorlagenLayout.KopfZeile;

        for (var i = 0; i < headers.Length; i++)
            worksheet.Cell(headerRow, i + 1).Value = headers[i];

        workbook.SaveAs(path);
    }
}
