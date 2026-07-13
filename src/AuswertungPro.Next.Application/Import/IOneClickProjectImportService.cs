using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Erkanntes Quellformat des vollständigen Kanalfernseh-Imports.</summary>
public enum OneClickProjectImportFormat
{
    Unknown,
    Ikas,
    Ibak,
    WinCan,
    Ambiguous,
    Kins
}

/// <summary>Ergebnis des vollständigen Kanalfernseh-Imports.</summary>
public sealed record OneClickProjectImportResult(
    OneClickProjectImportFormat Format,
    int Found,
    int Created,
    int Updated,
    int Errors,
    int Conflicts,
    IReadOnlyList<string> Messages);

/// <summary>Importiert einen vollständigen Kanalfernseh-Projektordner.</summary>
public interface IOneClickProjectImportService
{
    OneClickProjectImportResult Import(
        string sourceFolder,
        string projectFolder,
        Project project,
        ImportRunContext? context = null);
}
