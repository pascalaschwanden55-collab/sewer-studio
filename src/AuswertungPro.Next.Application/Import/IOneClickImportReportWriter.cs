namespace AuswertungPro.Next.Application.Import;

/// <summary>Schreibt den nachvollziehbaren Textbericht eines Ein-Knopf-Imports.</summary>
public interface IOneClickImportReportWriter
{
    /// <returns>Pfad des geschriebenen Berichts, <c>null</c> wenn er nicht geschrieben werden konnte.</returns>
    string? TryWrite(string projectFolder, OneClickProjectImportResult result);
}
