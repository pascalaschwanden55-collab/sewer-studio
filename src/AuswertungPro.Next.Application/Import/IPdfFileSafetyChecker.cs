namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis der Groessenpruefung einer PDF-Datei.</summary>
public sealed record PdfFileSafetyResult(bool Allowed, string? Message);

/// <summary>Prueft Existenz und Dateigroesse, bevor eine PDF verarbeitet wird.</summary>
public interface IPdfFileSafetyChecker
{
    long ResolveMaxBytes();

    PdfFileSafetyResult CheckFileBudget(string pdfPath, long? maxBytes = null);

    void ThrowIfFileTooLarge(string pdfPath, long? maxBytes = null);
}
