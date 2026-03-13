using System;
using System.IO;
using UglyToad.PdfPig;

var pdfPath = args.Length > 0 ? args[0] : "E:\\Haltung_1.15\\10.2025-80811\\20251006_10.2025-80811.pdf";

if (!File.Exists(pdfPath))
{
    Console.WriteLine($"PDF nicht gefunden: {pdfPath}");
    return 1;
}

Console.WriteLine($"Analysiere: {pdfPath}\n");

using var doc = PdfDocument.Open(pdfPath);
Console.WriteLine($"Seiten: {doc.NumberOfPages}\n");

var page = doc.GetPage(1);
Console.WriteLine($"=== Seite 1 Text ({page.Text.Length} Zeichen) ===");
Console.WriteLine(page.Text);

return 0;
