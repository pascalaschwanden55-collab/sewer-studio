// PdfCodeScanner — Zaehlt Befunde eines Schadencode-Praefixes je Haltungsordner
// ueber den ECHTE Importpfad (LegacyPdfImportService), nicht per Text-Regex:
// Damit sind die deutschen Protokollbeschreibungen (Fretz, KIT, KINS, Pallon,
// IBAK) korrekt abgedeckt. Zusaetzlich eingebettete Fotos je Ordner.
// Schreibfrei: Kundenoriginale werden nur gelesen. Ausgabe als JSON.
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig;

if (args.Length < 2 || args[0] != "--class")
{
    Console.WriteLine("Verwendung: PdfCodeScanner --class BAH [--root D:\\Haltungen] [--out scan.json]");
    return 1;
}

var codePrefix = args[1].Trim().ToUpperInvariant();
string rootPath = @"D:\Haltungen";
string? outPath = null;
for (var i = 2; i < args.Length; i++)
{
    if (args[i] == "--root" && i + 1 < args.Length) rootPath = args[++i];
    else if (args[i] == "--out" && i + 1 < args.Length) outPath = args[++i];
}

var records = new List<Dictionary<string, object?>>();
var folders = Directory.GetDirectories(rootPath);
Console.WriteLine($"Scanne {folders.Length} Haltungsordner unter {rootPath} ueber den Importpfad, Praefix {codePrefix} ...");

var importer = new LegacyPdfImportService();
var scanned = 0;
foreach (var folder in folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
{
    var holding = Path.GetFileName(folder);
    string[] pdfs;
    try
    {
        pdfs = Directory.GetFiles(folder, "*.pdf", SearchOption.AllDirectories);
    }
    catch (Exception)
    {
        continue;
    }

    var codes = new SortedSet<string>(StringComparer.Ordinal);
    var befunde = 0;
    var fotos = 0;
    var pdfsOk = 0;
    var pdfsFehler = 0;
    foreach (var pdf in pdfs)
    {
        // Befunde ueber den echten Importpfad
        try
        {
            var project = new Project();
            var stats = importer.ImportPdf(pdf, project, explicitPdfToTextPath: null);
            if (stats.Errors == 0)
            {
                pdfsOk++;
                foreach (var record in project.Data)
                {
                    foreach (var finding in record.VsaFindings)
                    {
                        var code = (finding.KanalSchadencode ?? string.Empty).Trim().ToUpperInvariant();
                        if (code.StartsWith(codePrefix, StringComparison.Ordinal))
                        {
                            codes.Add(code);
                            befunde++;
                        }
                    }
                }
            }
            else
            {
                pdfsFehler++;
            }
        }
        catch (Exception)
        {
            pdfsFehler++;
        }

        // Fotos zaehlen (PdfPig, dekodiert nichts weiter)
        try
        {
            using var doc = PdfDocument.Open(pdf);
            foreach (var page in doc.GetPages())
            {
                try
                {
                    fotos += page.GetImages().Count(img =>
                        img.WidthInSamples >= 200 && img.HeightInSamples >= 150);
                }
                catch { /* einzelne Bildliste unkritisch */ }
            }
        }
        catch { /* PDF-Bildzaehlung fehlgeschlagen ist unkritisch */ }
    }

    scanned++;
    if (scanned % 200 == 0) Console.WriteLine($"  ... {scanned}/{folders.Length}");
    if (codes.Count == 0 && fotos == 0) continue;
    records.Add(new Dictionary<string, object?>
    {
        ["haltung"] = holding,
        ["codes"] = codes.ToArray(),
        ["befunde_mit_praefix"] = befunde,
        ["fotos"] = fotos,
        ["pdfs"] = pdfs.Length,
        ["pdfs_ok"] = pdfsOk,
        ["pdfs_fehler"] = pdfsFehler,
    });
}

var output = JsonSerializer.Serialize(new Dictionary<string, object?>
{
    ["klasse"] = codePrefix,
    ["stamm"] = rootPath,
    ["ordner_gescannt"] = folders.Length,
    ["treffer"] = records.Count,
    ["ergebnisse"] = records,
}, new JsonSerializerOptions { WriteIndented = true });

if (outPath is not null)
{
    File.WriteAllText(outPath, output);
    Console.WriteLine($"Ergebnis: {outPath}");
}
else
{
    Console.WriteLine(output);
}
Console.WriteLine($"Treffer-Ordner: {records.Count}");
return 0;
