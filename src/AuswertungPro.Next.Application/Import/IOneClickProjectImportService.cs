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
    IReadOnlyList<string> Messages)
{
    // Additiv fuer das Plausibilitaetstor — siehe ImportStats. Found taugt nicht als
    // Pruefgroesse, weil dort auch Schaechte mitzaehlen.

    /// <summary>Haltungen, die die geprueften Quellen versprechen.</summary>
    public int ErwarteteHaltungen { get; init; }

    /// <summary>Tatsaechlich verarbeitete Haltungen, ohne Schaechte.</summary>
    public int BearbeiteteHaltungen { get; init; }

    /// <summary>Protokoll der geprueften Importquellen. Null = kein Urteil moeglich.</summary>
    public AuswertungPro.Next.Application.UseCases.Import.Quellen.QuellenwahlErgebnis? Quellenprotokoll { get; init; }
}

/// <summary>Importiert einen vollständigen Kanalfernseh-Projektordner.</summary>
public interface IOneClickProjectImportService
{
    OneClickProjectImportResult Import(
        string sourceFolder,
        string projectFolder,
        Project project,
        ImportRunContext? context = null);
}
