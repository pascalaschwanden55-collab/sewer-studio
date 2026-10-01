// AuswertungPro – KI Videoanalyse Modul
using System.Globalization;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

/// <summary>
/// Reine Hilfsmethoden fuer die Erkennung und Korrektur von Custom-Font-Encoding in PDF-Texten.
/// Kein PdfPig-Abhaengigkeit, kein IO – nur reine Zeichenoperationen.
/// </summary>
internal static class PdfFontEncodingDecoder
{
    /// <summary>
    /// Bekannte Textanker, die in validen Protokoll-PDFs typischerweise vorkommen.
    /// </summary>
    internal static readonly string[] KnownTextAnchorWords =
    {
        "Leitung", "Video", "Foto", "Zustand", "Material",
        "Schacht", "Kanal", "Haltung", "Inspektion", "Dimension",
        "Profil", "Rohr", "Position", "Entf", "Strasse", "Wetter"
    };

    /// <summary>
    /// Erkennt PDFs mit verschobener Zeichencodierung (Custom Font Encoding)
    /// und korrigiert den Text automatisch. Manche PDF-Generatoren verwenden
    /// Schriften, bei denen alle Zeichen um einen festen Offset verschoben sind.
    /// PdfPig kann diese nicht korrekt decodieren.
    /// </summary>
    internal static string TryDecodeShiftedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var shift = DetectShift(text);
        return shift > 0
            ? ShiftAllChars(text, shift)
            : text;
    }

    /// <summary>
    /// Ermittelt den festen Unicode-Offset eines verschobenen PDF-Fonts.
    /// Null bedeutet, dass kein ausreichend sicherer Shift erkannt wurde.
    /// </summary>
    internal static int DetectShift(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        int existingMatches = CountWordMatches(text, KnownTextAnchorWords);
        if (existingMatches >= 3)
            return 0;

        int bestShift = 0;
        int bestCount = existingMatches;

        for (int shift = 1; shift <= 60; shift++)
        {
            var decoded = ShiftAllChars(text, shift);
            int count = CountWordMatches(decoded, KnownTextAnchorWords);
            if (count > bestCount)
            {
                bestCount = count;
                bestShift = shift;
            }
        }

        if (bestShift > 0 && bestCount >= 3)
            return bestShift;

        return 0;
    }

    /// <summary>
    /// Prueft ob ein Text aufgrund hohen Steuerzeichenanteils und fehlender Textanker
    /// nicht decodierbar ist (Custom Font Encoding ohne Offset-Korrektur).
    /// </summary>
    internal static bool LooksLikeUndecodableFontEncoding(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var chars = text.Where(ch => !char.IsWhiteSpace(ch)).ToList();
        if (chars.Count < 80)
            return false;

        if (CountWordMatches(text, KnownTextAnchorWords) > 0)
            return false;

        var suspiciousChars = chars.Count(IsSuspiciousDecodedChar);
        return suspiciousChars / (double)chars.Count >= 0.25;
    }

    /// <summary>
    /// Gibt true zurueck, wenn das Zeichen ein Hinweis auf falsch decodiertes Font-Encoding ist
    /// (Steuerzeichen, nicht zugeordnete Unicode-Kategorie, PrivateUse, Surrogate).
    /// </summary>
    internal static bool IsSuspiciousDecodedChar(char ch)
    {
        if (char.IsControl(ch))
            return true;

        return char.GetUnicodeCategory(ch) switch
        {
            UnicodeCategory.Control => true,
            UnicodeCategory.OtherNotAssigned => true,
            UnicodeCategory.PrivateUse => true,
            UnicodeCategory.Surrogate => true,
            _ => false
        };
    }

    /// <summary>
    /// Zaehlt wie viele der angegebenen Woerter (case-insensitive) im Text vorkommen.
    /// </summary>
    internal static int CountWordMatches(string text, string[] words)
    {
        int count = 0;
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Verschiebt alle druckbaren Zeichen im Text um den angegebenen Unicode-Offset.
    /// Leerzeichen, Tab, CR und LF bleiben unveraendert.
    /// </summary>
    internal static string ShiftAllChars(string text, int shift)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                sb.Append(ch);
            else
                sb.Append((char)(ch + shift));
        }
        return sb.ToString();
    }
}
