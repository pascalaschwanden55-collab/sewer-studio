using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

public sealed record ImportStats(
    int Found,
    int Created,
    int Updated,
    int Errors,
    int Uncertain,
    IReadOnlyList<string> Messages
)
{
    // Additive Felder fuer das Plausibilitaetstor. Bewusst als init-Eigenschaften mit
    // Standardwert, damit alle bestehenden Aufrufer unveraendert bleiben.
    //
    // Warum nicht Found? Found zaehlt bei WinCan auch Schaechte mit
    // (WinCanDbImportService.Records.cs, found++ in der Knotenschleife). Gemessen:
    // Found = 44 bei 15 Haltungen und 26 Schaechten. Als Pruefgroesse unbrauchbar.

    /// <summary>Haltungen, die die geprueften Quellen versprechen. 0 = keine Angabe.</summary>
    public int ErwarteteHaltungen { get; init; }

    /// <summary>Tatsaechlich verarbeitete Haltungen, ohne Schaechte. 0 = keine Angabe.</summary>
    public int BearbeiteteHaltungen { get; init; }

    /// <summary>
    /// Protokoll der geprueften Importquellen. Leer = dieser Weg liefert kein Protokoll;
    /// das Plausibilitaetstor urteilt dann nicht.
    /// </summary>
    public AuswertungPro.Next.Application.UseCases.Import.Quellen.QuellenwahlErgebnis? Quellenprotokoll { get; init; }
}

public interface IPdfImportService
{
    Result<ImportStats> ImportPdf(string pdfPath, Project project, string? pdfToTextPath, bool fillMissingOnly = false, ImportRunContext? ctx = null);
}

public interface IXtfImportService
{
    Result<ImportStats> ImportXtfFiles(IEnumerable<string> xtfPaths, Project project, ImportRunContext? ctx = null);
}

public interface IWinCanDbImportService
{
    Result<ImportStats> ImportWinCanExport(string exportRoot, Project project, ImportRunContext? ctx = null);
}

public interface IIbakImportService
{
    Result<ImportStats> ImportIbakExport(string exportRoot, Project project, ImportRunContext? ctx = null);
}

public interface IKinsImportService
{
    Result<ImportStats> ImportKinsExport(string exportRoot, Project project, ImportRunContext? ctx = null);
}

public interface ISchachtProImportService
{
    Result<ImportStats> ImportSchachtProArchive(string sproPath, Project project, ImportRunContext? ctx = null);
}
