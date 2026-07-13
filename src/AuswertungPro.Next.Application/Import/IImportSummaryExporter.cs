using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Schreibt einen CSV-Nachweis der importierten Haltungs- und Schachtdaten.</summary>
public interface IImportSummaryExporter
{
    string Export(string projectPath, Project project);
}
