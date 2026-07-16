using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Kompatible Fassade; Datei- und PdfPig-Arbeit liegt im Instanzdienst.</summary>
public static class PdfFormFieldExtractor
{
    private static readonly IPdfFormFieldReader Default = new PdfFormFieldReaderService();

    public static IPdfFormFieldReader Current => Default;

    [Obsolete("Die PDF-Formularfeld-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IPdfFormFieldReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        throw new NotSupportedException(
            "Die PDF-Formularfeld-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static IReadOnlyList<PdfFormFieldEntry> GetPageFieldEntries(string pdfPath, int pageNumber)
        => Current.GetPageFieldEntries(pdfPath, pageNumber);
}
