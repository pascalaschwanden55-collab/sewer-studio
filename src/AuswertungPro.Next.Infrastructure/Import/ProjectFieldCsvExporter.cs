using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Exportiert alle Felder eines Projekts (Haltungen + Schaechte) als CSV-Datei
/// in den Ordner &lt;projectDir&gt;/__IMPORT_REPORTS/import_summary_&lt;stamp&gt;.csv.
/// </summary>
public static class ProjectFieldCsvExporter
{
    private static readonly ImportSummaryExporter DefaultExporter = new();

    /// <summary>
    /// Schreibt die CSV-Datei und gibt den erstellten Pfad zurueck.
    /// </summary>
    /// <param name="project">Aktives Projekt.</param>
    /// <param name="reportDir">Zielordner; wird ggf. angelegt.</param>
    /// <returns>Vollstaendiger Pfad der erstellten CSV-Datei.</returns>
    public static string Export(Project project, string reportDir)
        => DefaultExporter.ExportToDirectory(project, reportDir);

    /// <summary>
    /// CSV-Escape-Logik (Semikolon, Anfuehrungszeichen, Zeilenumbrueche).
    /// </summary>
    public static string Escape(string? v)
        => ImportSummaryExporter.Escape(v);
}
