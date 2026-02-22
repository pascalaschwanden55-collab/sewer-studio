using System.Globalization;
using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Export;

/// <summary>
/// Platzhalter: exportiert CSV. Später ersetzen durch euren Excel-Template-Export.
/// </summary>
public sealed class CsvExcelExportService : IExcelExportService
{
    public Result ExportToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", FieldCatalog.ColumnOrder));

            foreach (var rec in project.Data)
            {
                var values = FieldCatalog.ColumnOrder
                    .Select(c => Escape(rec.GetFieldValue(c)))
                    .ToArray();
                sb.AppendLine(string.Join(";", values));
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail("EXP-CSV", ex.Message);
        }
    }

    public Result ExportSchaechteToTemplate(Project project, string templatePath, string outputPath, int headerRow, int startRow)
    {
        try
        {
            var headers = project.SchaechteData
                .SelectMany(r => r.Fields.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(";", headers.Select(Escape)));

            foreach (var rec in project.SchaechteData)
            {
                var values = headers
                    .Select(h => Escape(rec.GetFieldValue(h)))
                    .ToArray();
                sb.AppendLine(string.Join(";", values));
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail("EXP-CSV-SCHACHT", ex.Message);
        }
    }

    private static string Escape(string? s)
    {
        s ??= "";
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }
}
