using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Exportiert alle Felder eines Projekts (Haltungen + Schaechte) als CSV-Datei
/// in den Ordner &lt;projectDir&gt;/__IMPORT_REPORTS/import_summary_&lt;stamp&gt;.csv.
/// </summary>
public static class ProjectFieldCsvExporter
{
    private const string Header = "Type;RecordId;Field;Value;Source;UserEdited;LastUpdatedUtc";

    /// <summary>
    /// Schreibt die CSV-Datei und gibt den erstellten Pfad zurueck.
    /// </summary>
    /// <param name="project">Aktives Projekt.</param>
    /// <param name="reportDir">Zielordner; wird ggf. angelegt.</param>
    /// <returns>Vollstaendiger Pfad der erstellten CSV-Datei.</returns>
    public static string Export(Project project, string reportDir)
    {
        Directory.CreateDirectory(reportDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(reportDir, $"import_summary_{stamp}.csv");

        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var rec in project.Data)
        {
            foreach (var field in FieldCatalog.ColumnOrder)
            {
                var value = rec.GetFieldValue(field) ?? "";
                var meta = rec.FieldMeta.TryGetValue(field, out var m) ? m : null;
                sb.AppendLine(string.Join(";",
                    "Haltung",
                    rec.Id,
                    Escape(field),
                    Escape(value),
                    meta?.Source.ToString() ?? "",
                    meta?.UserEdited.ToString() ?? "",
                    meta?.LastUpdatedUtc.ToString("o") ?? ""));
            }
        }

        foreach (var schacht in project.SchaechteData)
        {
            foreach (var kv in schacht.Fields)
            {
                sb.AppendLine(string.Join(";",
                    "Schacht",
                    schacht.Id,
                    Escape(kv.Key),
                    Escape(kv.Value ?? ""),
                    "",
                    "",
                    schacht.ModifiedAtUtc.ToString("o")));
            }
        }

        AtomicTextFileWriter.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    /// <summary>
    /// CSV-Escape-Logik (Semikolon, Anfuehrungszeichen, Zeilenumbrueche).
    /// </summary>
    public static string Escape(string? v)
    {
        v ??= "";
        if (v.Contains(';') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
