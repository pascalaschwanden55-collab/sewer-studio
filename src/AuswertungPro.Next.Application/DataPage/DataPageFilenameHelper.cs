using System.IO;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Dateinamen-Hilfsklasse fuer die DataPage.
/// Aus <c>DataPageViewModel.SanitizeFilenamePart</c> extrahiert (verhaltensneutral).
/// </summary>
public static class DataPageFilenameHelper
{
    /// <summary>
    /// Bereinigt einen String so, dass er als Dateinamen-Teil verwendet werden kann.
    /// Ungueltige Zeichen werden durch Unterstriche ersetzt.
    /// Gibt "unknown" zurueck bei leerer Eingabe.
    /// </summary>
    public static string SanitizeFilenamePart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        foreach (var c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');

        return text.Trim();
    }
}
