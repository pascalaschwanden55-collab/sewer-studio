// PdfImageAnalyzer — Listet alle Bilder eines PDFs mit Breite, Hoehe und PNG-Groesse
// Nutzt UglyToad.PdfPig (bereits im Projekt vorhanden)
// Zeigt auch RawBytes-Laenge und ColorSpace fuer fehlgeschlagene PNG-Konvertierungen

using System;
using System.IO;
using System.Reflection;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

if (args.Length == 0)
{
    Console.WriteLine("Verwendung: PdfImageAnalyzer <pdf-pfad> [pdf-pfad2] ...");
    Console.WriteLine("  Listet alle Bilder pro Seite mit Breite, Hoehe, PNG-Bytes.");
    return 1;
}

// JSON-Modus (additiv): maschinenlesbare Bildliste mit Rohbyte-SHA-256.
// Dient dem Gold-Bildbeleg (bytegleicher Abgleich Goldbild vs. PDF-Strom).
if (args[0] == "--json" || args[0] == "--normalize-json")
{
    var withNormalization = args[0] == "--normalize-json";
    var records = new List<Dictionary<string, object?>>();
    foreach (var pdfPath in args.Skip(1))
    {
        if (!File.Exists(pdfPath))
        {
            records.Add(new() { ["pdf"] = pdfPath, ["error"] = "Datei nicht gefunden" });
            continue;
        }
        try
        {
            using var doc = PdfDocument.Open(pdfPath);
            foreach (var page in doc.GetPages())
            {
                var idx = 0;
                foreach (var img in page.GetImages())
                {
                    idx++;
                    byte[] raw = Array.Empty<byte>();
                    try { raw = img.RawBytes.ToArray(); } catch { }
                    string? csType = null;
                    int? csComponents = null;
                    try
                    {
                        csType = img.ColorSpaceDetails?.Type.ToString();
                        csComponents = img.ColorSpaceDetails?.NumberOfColorComponents;
                    }
                    catch { }

                    string? normalizedSha = null;
                    string? normalizationStatus = null;
                    if (withNormalization && raw.Length > 0
                        && PdfImageAnalyzer.PdfJpegColorNormalizer.HasCompleteJpegEnvelope(raw))
                    {
                        if (PdfImageAnalyzer.PdfJpegColorNormalizer.TryCreateRequest(
                                img, raw, out var request))
                        {
                            if (request is null)
                            {
                                normalizationStatus = "blocked_fail_closed";
                            }
                            else if (PdfImageAnalyzer.PdfJpegColorNormalizer.TryNormalizeToRgbPng(
                                         request, out var png)
                                     && png.Length > 0)
                            {
                                normalizedSha = Convert.ToHexString(
                                    System.Security.Cryptography.SHA256.HashData(png)).ToLowerInvariant();
                                normalizationStatus = "normalized";
                            }
                            else
                            {
                                normalizationStatus = "failed";
                            }
                        }
                        else
                        {
                            normalizationStatus = "not_required";
                        }
                    }

                    records.Add(new()
                    {
                        ["pdf"] = pdfPath,
                        ["page"] = page.Number,
                        ["index"] = idx,
                        ["width"] = (int)img.WidthInSamples,
                        ["height"] = (int)img.HeightInSamples,
                        ["bitsPerComponent"] = img.BitsPerComponent,
                        ["colorSpace"] = csType,
                        ["colorComponents"] = csComponents,
                        ["rawBytes"] = raw.Length,
                        ["rawSha256"] = raw.Length > 0
                            ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw)).ToLowerInvariant()
                            : null,
                        ["jpegEnvelope"] = raw.Length >= 4
                                           && raw[0] == 0xff && raw[1] == 0xd8
                                           && raw[^2] == 0xff && raw[^1] == 0xd9,
                        ["normalizedSha256"] = normalizedSha,
                        ["normalizationStatus"] = normalizationStatus,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            records.Add(new() { ["pdf"] = pdfPath, ["error"] = ex.Message });
        }
    }
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(records));
    return 0;
}

// Statistik ueber alle PDFs
int totalImages = 0;
int totalPngOk = 0;
int totalPngFail = 0;
var allSizes = new List<(int w, int h, int pngBytes, int rawBytes, string file, int page, string cat)>();

foreach (var pdfPath in args)
{
    if (!File.Exists(pdfPath))
    {
        Console.WriteLine($"FEHLER: Datei nicht gefunden: {pdfPath}");
        continue;
    }

    Console.WriteLine($"\n{'=',-80}");
    Console.WriteLine($"PDF: {Path.GetFileName(pdfPath)}");
    Console.WriteLine($"Pfad: {pdfPath}");
    Console.WriteLine($"Groesse: {new FileInfo(pdfPath).Length:N0} Bytes");
    Console.WriteLine($"{'=',-80}");

    try
    {
        using var doc = PdfDocument.Open(pdfPath);
        Console.WriteLine($"Seiten: {doc.NumberOfPages}\n");

        Console.WriteLine($"{"S",-3} {"#",-3} {"Breite",-7} {"Hoehe",-7} {"Bpp",-4} {"RawBytes",-11} {"PNG-Bytes",-11} {"PNG-KB",-9} {"ColorSpace",-16} {"Kategorie"}");
        Console.WriteLine(new string('-', 100));

        int imgIdx = 0;
        foreach (var page in doc.GetPages())
        {
            foreach (var img in page.GetImages())
            {
                totalImages++;
                imgIdx++;

                int w = (int)img.WidthInSamples;
                int h = (int)img.HeightInSamples;
                int bpp = img.BitsPerComponent;

                // RawBytes auslesen
                int rawLen = 0;
                try { rawLen = img.RawBytes.Count; } catch { }

                // ColorSpace per Reflection holen (nicht immer public)
                string colorSpace = "?";
                try
                {
                    var csProp = img.GetType().GetProperty("ColorSpace", BindingFlags.Public | BindingFlags.Instance);
                    if (csProp != null)
                    {
                        var csVal = csProp.GetValue(img);
                        colorSpace = csVal?.ToString() ?? "null";
                    }
                }
                catch { }

                string pngInfo;
                string category;
                int pngLen = 0;

                if (img.TryGetPng(out var pngBytes))
                {
                    totalPngOk++;
                    pngLen = pngBytes.Length;
                    pngInfo = $"{pngLen,10:N0} {pngLen / 1024.0,7:F1}";

                    // Kategorisierung
                    if (w < 50 || h < 50)
                        category = "ICON";
                    else if (w < 300 || h < 200)
                        category = "KLEIN/LOGO";
                    else if (pngLen < 15_000)
                        category = "ZEICHNUNG";
                    else
                        category = "FOTO";
                }
                else
                {
                    totalPngFail++;
                    pngInfo = "FEHLER          ";
                    // Fuer fehlgeschlagene: pruefen ob es JPEG-Daten sein koennten
                    bool isJpeg = false;
                    try
                    {
                        var raw = img.RawBytes;
                        if (raw.Count >= 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF)
                            isJpeg = true;
                    }
                    catch { }
                    category = isJpeg ? "JPEG-RAW" : "PNG-FAIL";
                }

                Console.WriteLine($"{page.Number,-3} {imgIdx,-3} {w,-7} {h,-7} {bpp,-4} {rawLen,10:N0} {pngInfo,-20} {colorSpace,-16} {category}");

                allSizes.Add((w, h, pngLen, rawLen, Path.GetFileName(pdfPath), page.Number, category));
            }
        }

        if (imgIdx == 0)
            Console.WriteLine("  (Keine Bilder gefunden)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FEHLER beim Oeffnen: {ex.Message}");
    }
}

// Zusammenfassung
Console.WriteLine($"\n\n{'=',-80}");
Console.WriteLine("ZUSAMMENFASSUNG");
Console.WriteLine($"{'=',-80}");
Console.WriteLine($"Bilder gesamt:        {totalImages}");
Console.WriteLine($"PNG erfolgreich:      {totalPngOk}");
Console.WriteLine($"PNG fehlgeschlagen:   {totalPngFail}");

if (allSizes.Count > 0)
{
    // Kategorien-Uebersicht
    Console.WriteLine("\n--- Kategorien ---");
    foreach (var g in allSizes.GroupBy(s => s.cat).OrderByDescending(g => g.Count()))
        Console.WriteLine($"  {g.Key,-16}: {g.Count(),4} Bilder");

    // JPEG-RAW Groessen
    var jpegRaw = allSizes.Where(s => s.cat == "JPEG-RAW").ToList();
    if (jpegRaw.Count > 0)
    {
        Console.WriteLine($"\n--- JPEG-RAW Bilder (TryGetPng fehlgeschlagen, JPEG-Header erkannt) ---");
        Console.WriteLine($"Anzahl: {jpegRaw.Count}");
        Console.WriteLine($"Typische Groessen:");
        foreach (var g in jpegRaw.GroupBy(s => $"{s.w}x{s.h}").OrderByDescending(g => g.Count()).Take(10))
        {
            var avgRaw = g.Average(s => s.rawBytes);
            Console.WriteLine($"  {g.Key,-14}: {g.Count(),4} Bilder, Durchschnitt RawBytes: {avgRaw:N0} ({avgRaw/1024.0:F1} KB)");
        }
    }

    // PNG-FAIL (weder JPEG noch PNG)
    var pngFail = allSizes.Where(s => s.cat == "PNG-FAIL").ToList();
    if (pngFail.Count > 0)
    {
        Console.WriteLine($"\n--- PNG-FAIL Bilder (kein JPEG-Header, TryGetPng fehlgeschlagen) ---");
        Console.WriteLine($"Anzahl: {pngFail.Count}");
        foreach (var g in pngFail.GroupBy(s => $"{s.w}x{s.h}").OrderByDescending(g => g.Count()).Take(10))
        {
            var avgRaw = g.Average(s => s.rawBytes);
            Console.WriteLine($"  {g.Key,-14}: {g.Count(),4} Bilder, Durchschnitt RawBytes: {avgRaw:N0} ({avgRaw/1024.0:F1} KB)");
        }
    }

    // Nur erfolgreiche PNGs
    var pngOk = allSizes.Where(s => s.pngBytes > 0).ToList();
    if (pngOk.Count > 0)
    {
        Console.WriteLine($"\n--- Groessenverteilung (nur erfolgreiche PNGs: {pngOk.Count}) ---");

        // Breiten-Verteilung
        Console.WriteLine("\nBreite (px):");
        foreach (var g in pngOk.GroupBy(s => s.w < 100 ? "<100" : s.w < 300 ? "100-299" : s.w < 600 ? "300-599" : s.w < 1000 ? "600-999" : "1000+").OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key,-10}: {g.Count(),4} Bilder");

        // PNG-Bytes-Verteilung
        Console.WriteLine("\nPNG-Groesse:");
        foreach (var g in pngOk.GroupBy(s => s.pngBytes < 1_000 ? "<1KB" : s.pngBytes < 5_000 ? "1-5KB" : s.pngBytes < 15_000 ? "5-15KB" : s.pngBytes < 50_000 ? "15-50KB" : s.pngBytes < 200_000 ? "50-200KB" : "200KB+").OrderBy(g => g.Key))
            Console.WriteLine($"  {g.Key,-10}: {g.Count(),4} Bilder");
    }
}

return 0;
