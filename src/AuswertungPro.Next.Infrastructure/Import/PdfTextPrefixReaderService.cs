using UglyToad.PdfPig;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

public sealed class PdfTextPrefixReaderService : IPdfTextPrefixReader
{
    public string? ReadPdfTextPrefix(string path, int maxPages = 6)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            return string.Join(
                "\n",
                document.GetPages()
                    .Take(Math.Max(1, maxPages))
                    .Select(page => page.Text));
        }
        catch
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
