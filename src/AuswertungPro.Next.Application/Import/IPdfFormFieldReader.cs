namespace AuswertungPro.Next.Application.Import;

/// <summary>Ein ausgelesener Wert eines interaktiven PDF-Formularfelds.</summary>
public sealed record PdfFormFieldEntry(
    int? PageNumber,
    string? PartialName,
    string? AlternateName,
    string? MappingName,
    string Value);

/// <summary>Liest die Formularfelder einer einzelnen PDF-Seite.</summary>
public interface IPdfFormFieldReader
{
    IReadOnlyList<PdfFormFieldEntry> GetPageFieldEntries(string pdfPath, int pageNumber);
}
