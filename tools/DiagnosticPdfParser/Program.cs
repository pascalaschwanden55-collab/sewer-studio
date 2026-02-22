using System;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

var pdfPath = args.Length > 0 
    ? args[0] 
    : @"E:\GEP_Altdorf_2025_Zone_1.15_29261_925_Export\GEP_Altdorf_2025_Zone_1.15_29261_925.pdf";

var importMode = args.Any(a => string.Equals(a, "--import", StringComparison.OrdinalIgnoreCase));
var pageNumber = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 1;

if (!File.Exists(pdfPath))
{
    Console.WriteLine($"PDF nicht gefunden: {pdfPath}");
    return 1;
}

Console.WriteLine($"Analysiere PDF: {pdfPath}\n");

if (importMode)
{
    var project = new Project();
    var svc = new LegacyPdfImportService();
    var stats = svc.ImportPdf(pdfPath, project, explicitPdfToTextPath: null);

    Console.WriteLine("=== Import-Stats ===");
    Console.WriteLine($"Found={stats.Found} Created={stats.CreatedRecords} Updated={stats.UpdatedRecords} Errors={stats.Errors} Uncertain={stats.Uncertain}");
    Console.WriteLine($"Records im Projekt: {project.Data.Count}");
    Console.WriteLine();

    foreach (var rec in project.Data.Take(20))
    {
        var hn = rec.GetFieldValue("Haltungsname");
        var d = rec.GetFieldValue("Datum_Jahr");
        var s = rec.GetFieldValue("Strasse");
        Console.WriteLine($"Haltungsname='{hn}' Datum='{d}' Strasse='{s}'");
    }

    var placeholderCount = project.Data.Count(r =>
    {
        var key = (r.GetFieldValue("Haltungsname") ?? "").Trim();
        return string.Equals(key, "Datum :", StringComparison.OrdinalIgnoreCase)
               || key.StartsWith("UNBEKANNT_", StringComparison.OrdinalIgnoreCase)
               || key.IndexOf("Wetter :", StringComparison.OrdinalIgnoreCase) >= 0;
    });
    Console.WriteLine($"\nPlaceholder-Keys: {placeholderCount}");
    return 0;
}

// Lese spezifische Seite
string pageText;
using (var doc = PdfDocument.Open(pdfPath))
{
    if (doc.NumberOfPages < 1)
    {
        Console.WriteLine("❌ PDF hat keine Seiten");
        return 1;
    }
    
    if (pageNumber < 1 || pageNumber > doc.NumberOfPages)
    {
        Console.WriteLine($"❌ Seite {pageNumber} existiert nicht (PDF hat {doc.NumberOfPages} Seiten)");
        return 1;
    }
    
    var page = doc.GetPage(pageNumber);
    pageText = page.Text;
    Console.WriteLine($"Anzahl Seiten: {doc.NumberOfPages}");
    Console.WriteLine($"Textlänge Seite {pageNumber}: {pageText.Length} Zeichen\n");
    
    // Zeige erste 40 Zeilen
    var lines = pageText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    Console.WriteLine("=== Erste 40 Zeilen (ignoriere Zeile 0) ===\n");
    for (int i = 1; i < Math.Min(41, lines.Length); i++)
    {
        var line = lines[i].Trim();
        if (!string.IsNullOrWhiteSpace(line))
        {
            Console.WriteLine($"{i}: {line}");
        }
    }
    
    Console.WriteLine("\n=== Zeilen mit 'Haltung', 'Datum', 'Schacht', 'Inspektion' ===\n");
    for (int i = 0; i < Math.Min(60, lines.Length); i++)
    {
        var line = lines[i].Trim();
        if (line.IndexOf("Haltung", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Datum", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Schacht", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("Inspektion", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Console.WriteLine($"{i}: {line}");
        }
    }
}

Console.WriteLine("\n=== Parsing-Ergebnis ===\n");

// Nutze HoldingFolderDistributor zum Parsen
var parsed = HoldingFolderDistributor.ParsePdfPage(pageText);

if (!parsed.Success)
{
    Console.WriteLine($"❌ Parsing fehlgeschlagen: {parsed.Message}");
    return 1;
}

Console.WriteLine("✅ Parsing erfolgreich!");
Console.WriteLine($"   Haltung: {parsed.Haltung}");
Console.WriteLine($"   Datum: {parsed.Date?.ToString("dd.MM.yyyy") ?? "N/A"}");
Console.WriteLine($"   Video-Dateiname: {parsed.VideoFile}");

if (!string.IsNullOrEmpty(parsed.Message))
{
    Console.WriteLine($"   Info: {parsed.Message}");
}

return 0;
