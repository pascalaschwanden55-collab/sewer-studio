using System.Text;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Begrenzt extrahierten PDF-Text, ohne zuerst die komplette Datei in den RAM zu laden.</summary>
internal static class PdfExtractedTextBudget
{
    internal const int MaxCharacters = 16_000_000;
    private const int BufferCharacters = 8_192;

    public static string ReadUtf8AtMost(string path, int maxCharacters = MaxCharacters)
    {
        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var builder = new StringBuilder(Math.Min(maxCharacters, 64 * 1024));
        var buffer = new char[Math.Min(BufferCharacters, maxCharacters)];

        while (builder.Length < maxCharacters)
        {
            var remaining = maxCharacters - builder.Length;
            var read = reader.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0)
                break;
            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    public static string TakeAtMost(string? text, int maxCharacters)
    {
        if (string.IsNullOrEmpty(text) || maxCharacters <= 0)
            return string.Empty;

        return text.Length <= maxCharacters ? text : text[..maxCharacters];
    }
}
