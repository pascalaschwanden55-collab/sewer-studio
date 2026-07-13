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

        return ProjectFieldCsvExporter.Export(
            project,
            Path.Combine(projectDirectory, "__IMPORT_REPORTS"));
    }
}
