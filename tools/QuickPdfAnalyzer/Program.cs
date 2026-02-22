using System;
using System.IO;
using UglyToad.PdfPig;
using AuswertungPro.Next.Infrastructure;

var pdfPath = args.Length > 0 ? args[0] : "E:\\Haltung_1.15\\10.2025-80811\\20251006_10.2025-80811.pdf";

if (!File.Exists(pdfPath))
{
    Console.WriteLine($"❌ PDF nicht gefunden: {pdfPath}");
    return 1;
}

Console.WriteLine($"📄 Analysiere: {Path.GetFileName(pdfPath)}\n");

using var doc = PdfDocument.Open(pdfPath);
var page = doc.GetPage(1);
var text = page.Text;

Console.WriteLine($"Seiten: {doc.NumberOfPages}");
Console.WriteLine($"Textlänge Seite 1: {text.Length} Zeichen\n");

// Show first 50 lines
var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
Console.WriteLine("=== Erste 50 Zeilen ===\n");
for (int i = 0; i < Math.Min(50, lines.Length); i++)
{
    if (!string.IsNullOrWhiteSpace(lines[i]))
        Console.WriteLine($"{i}: {lines[i].Trim()}");
}

// Parse with HoldingFolderDistributor
Console.WriteLine("\n=== Parsing-Ergebnis ===\n");
var parsed = HoldingFolderDistributor.ParsePdfPage(text);

if (parsed.Success)
{
    Console.WriteLine("✅ Parsing erfolgreich!");
    Console.WriteLine($"   Haltung: {parsed.Haltung}");
    Console.WriteLine($"   Datum: {parsed.Date?.ToString("dd.MM.yyyy") ?? "N/A"}");
    Console.WriteLine($"   Video: {parsed.VideoFile ?? "N/A"}");
    if (!string.IsNullOrEmpty(parsed.Message))
        Console.WriteLine($"   Info: {parsed.Message}");
}
else
{
    Console.WriteLine($"❌ Parsing fehlgeschlagen: {parsed.Message}");
    Console.WriteLine($"   Gefundenes Datum: {parsed.Date?.ToString("dd.MM.yyyy") ?? "N/A"}");
    Console.WriteLine($"   Gefundene Haltung: {parsed.Haltung ?? "N/A"}");
    Console.WriteLine($"   Gefundenes Video: {parsed.VideoFile ?? "N/A"}");
}

return 0;
