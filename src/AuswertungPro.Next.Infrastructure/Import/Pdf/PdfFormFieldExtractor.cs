using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Kompatible Fassade; Datei- und PdfPig-Arbeit liegt im Instanzdienst.</summary>
public static class PdfFormFieldExtractor
{
    private static IPdfFormFieldReader _current = new PdfFormFieldReaderService();

    public static IPdfFormFieldReader Current => Volatile.Read(ref _current);

    public static void Use(IPdfFormFieldReader reader)
        => Volatile.Write(ref _current, reader ?? throw new ArgumentNullException(nameof(reader)));

    public static IReadOnlyList<PdfFormFieldEntry> GetPageFieldEntries(string pdfPath, int pageNumber)
        => Current.GetPageFieldEntries(pdfPath, pageNumber);
}
