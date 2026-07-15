namespace AuswertungPro.Next.Application.Export;

/// <summary>Text und Herkunft einer Seite aus einer Verteil-PDF.</summary>
public sealed record DistributionPdfPage(int PageNumber, string Text, string SourcePath);

/// <summary>Liest PDF-Seiten fuer die Haltungs-, Schacht- und Dichtheitsverteilung.</summary>
public interface IDistributionPdfPageReader
{
    IReadOnlyList<DistributionPdfPage> ReadPages(string pdfPath);
}
