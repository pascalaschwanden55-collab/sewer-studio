namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Schreibt die menschen- und maschinenlesbaren Berichte eines Importlaufs.
/// </summary>
public interface IImportRunReportExporter
{
    string Export(ImportRunLog log, string reportDirectory);
}
