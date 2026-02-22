using System;
using System.Linq;
using UglyToad.PdfPig;

if (args.Length < 1)
{
    Console.WriteLine("Usage: PdfHeaderReader <pdf-path> [page-number]");
    return 1;
}

var pdfPath = args[0];
var pageNum = args.Length > 1 ? int.Parse(args[1]) : 35;

if (!File.Exists(pdfPath))
{
    Console.WriteLine($"PDF nicht gefunden: {pdfPath}");
    return 1;
}

using var doc = PdfDocument.Open(pdfPath);
Console.WriteLine($"=== Seite {pageNum} von {doc.NumberOfPages} ===\n");

var page = doc.GetPage(pageNum);
var text = page.Text;
var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

Console.WriteLine("Erste 30 Zeilen (Header-Analyse, oberste Zeile ignoriert):\n");
for (int i = 1; i < Math.Min(31, lines.Length); i++)  // Start bei i=1 um erste Zeile zu überspringen
{
    var line = lines[i].Trim();
    if (!string.IsNullOrWhiteSpace(line))
    {
        Console.WriteLine($"{i}: {line}");
    }
}

// Suche spezifisch nach "Haltung" und "Datum" Feldern
Console.WriteLine("\n=== Suche nach 'Haltung' und 'Datum' im Header ===\n");
for (int i = 0; i < Math.Min(50, lines.Length); i++)
{
    var line = lines[i].Trim();
    if (line.Contains("Haltung", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Datum", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Schacht", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Inspektion", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"{i}: {line}");
    }
}

return 0;
