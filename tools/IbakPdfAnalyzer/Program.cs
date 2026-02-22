using System;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

if (args.Length == 0)
{
    Console.WriteLine("Usage: IbakPdfAnalyzer <pdf-path> [page-number]");
    return 1;
}

var pdfPath = args[0];
if (!File.Exists(pdfPath))
{
    Console.WriteLine($"File not found: {pdfPath}");
    return 1;
}

int? specificPage = null;
if (args.Length > 1 && int.TryParse(args[1], out var pageNum))
{
    specificPage = pageNum;
}

using var document = PdfDocument.Open(pdfPath);
Console.WriteLine($"PDF: {Path.GetFileName(pdfPath)}");
Console.WriteLine($"Pages: {document.NumberOfPages}");
Console.WriteLine();

var startPage = specificPage ?? 1;
var endPage = specificPage ?? document.NumberOfPages;

for (int i = startPage; i <= Math.Min(endPage, document.NumberOfPages); i++)
{
    var page = document.GetPage(i);
    Console.WriteLine($"=== Page {i} ===");
    Console.WriteLine($"Text length: {page.Text.Length} chars");
    Console.WriteLine($"Letters: {page.Letters.Count()}");
    Console.WriteLine($"Images: {page.GetImages().Count()}");
    Console.WriteLine($"Width: {page.Width}, Height: {page.Height}");
    
    // Try to extract text with different methods
    if (page.Text.Length > 0)
    {
        Console.WriteLine("\nRaw Text preview:");
        var preview = page.Text.Length > 500 ? page.Text.Substring(0, 500) : page.Text;
        Console.WriteLine(preview);
        
        // Try to get actual words
        Console.WriteLine("\nWords extraction:");
        var words = page.GetWords().ToList();
        Console.WriteLine($"Total words: {words.Count}");
        if (words.Any())
        {
            var sample = string.Join(" ", words.Take(50).Select(w => w.Text));
            Console.WriteLine($"Sample: {sample}");
        }
    }
    else
    {
        Console.WriteLine("\n⚠️ No extractable text - likely scanned/image-based PDF");
        
        // Check if there are any visible words/letters
        if (page.Letters.Any())
        {
            Console.WriteLine($"Found {page.Letters.Count()} letters");
            var sample = string.Join("", page.Letters.Take(100).Select(l => l.Value));
            Console.WriteLine($"Sample: {sample}");
        }
        
        // Check images
        var images = page.GetImages().ToList();
        if (images.Any())
        {
            Console.WriteLine($"\nImages found: {images.Count}");
            foreach (var img in images)
            {
                Console.WriteLine($"  - {img.Bounds.Width}x{img.Bounds.Height}");
            }
        }
    }
    Console.WriteLine();
}

return 0;
