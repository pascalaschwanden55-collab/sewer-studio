namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Liest fuer die Dokumenttyp-Erkennung nur die ersten angeforderten PDF-Seiten.
/// </summary>
public interface IPdfTextPrefixReader
{
    string? ReadPdfTextPrefix(string path, int maxPages = 6);
}
