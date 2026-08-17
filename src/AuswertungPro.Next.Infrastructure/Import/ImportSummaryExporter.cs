using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Ordnet den zentralen CSV-Export dem Berichtsordner eines Projekts zu.</summary>
public sealed class ImportSummaryExporter : IImportSummaryExporter
{
    public string Export(string projectPath, Project project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentNullException.ThrowIfNull(project);

        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
            throw new InvalidOperationException("Der Projektordner konnte nicht ermittelt werden.");

        return ExportToDirectory(
            project,
            Path.Combine(projectDirectory, "__IMPORT_REPORTS"));
    }

    internal string ExportToDirectory(Project project, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(reportDirectory, $"import_summary_{stamp}.csv");

        var content = new StringBuilder();
        content.AppendLine("Type;RecordId;Field;Value;Source;UserEdited;LastUpdatedUtc");

        foreach (var record in project.Data)
        {
            foreach (var field in FieldCatalog.ColumnOrder)
            {
                var value = record.GetFieldValue(field) ?? "";
                var metadata = record.FieldMeta.TryGetValue(field, out var fieldMetadata)
                    ? fieldMetadata
                    : null;
                content.AppendLine(string.Join(";",
                    "Haltung",
                    record.Id,
                    Escape(field),
                    Escape(value),
                    metadata?.Source.ToString() ?? "",
                    metadata?.UserEdited.ToString() ?? "",
                    metadata?.LastUpdatedUtc.ToString("o") ?? ""));
            }
        }

        foreach (var shaft in project.SchaechteData)
        {
            foreach (var field in shaft.Fields)
            {
                content.AppendLine(string.Join(";",
                    "Schacht",
                    shaft.Id,
                    Escape(field.Key),
                    Escape(field.Value ?? ""),
                    "",
                    "",
                    shaft.ModifiedAtUtc.ToString("o")));
            }
        }

        AtomicTextFileWriter.WriteAllText(path, content.ToString(), Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// Delegiert an die zentrale <see cref="CsvCell"/>-Entschaerfung. Diese
    /// Umsetzung setzte frueher nur Anfuehrungszeichen und liess Formelanfaenge
    /// (<c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, Tab, CR) stehen — obwohl der
    /// Importbericht Feldwerte aus Kundendaten enthaelt und die Projektregel
    /// jeden halb umgesetzten Exportweg verbietet (Codeaudit 2026-08-17).
    /// Negative Zahlen bleiben dabei Zahlen; genau dafuer gibt es CsvCell.
    /// </summary>
    internal static string Escape(string? value)
        => CsvCell.Escape(value);
}
