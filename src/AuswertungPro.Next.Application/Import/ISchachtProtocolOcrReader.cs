namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis der begrenzten Texterkennung eines ganzen Schachtprotokolls.</summary>
public sealed record SchachtProtocolOcrReadResult(
    bool Success,
    string Text,
    int TotalPages,
    int ExtractedPages,
    string? Message);

/// <summary>
/// Liest einen Bild-Scan seitenweise per OCR und beachtet die eigene
/// Seitengrenze fuer Schachtprotokolle.
/// </summary>
public interface ISchachtProtocolOcrReader
{
    SchachtProtocolOcrReadResult TryRead(string pdfPath, int maxPages = 40);
}
