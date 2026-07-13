namespace AuswertungPro.Next.Application.Import;

/// <summary>Schreibt den nachvollziehbaren Textbericht eines Ein-Knopf-Imports.</summary>
public interface IOneClickImportReportWriter
{
    void TryWrite(string projectFolder, OneClickProjectImportResult result);
}
