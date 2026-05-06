using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docnet.Core;
using Docnet.Core.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// V4.3: OCR-Fallback fuer PDFs aus denen pdftotext/PdfPig keinen brauchbaren Text liefert.
/// Deckt zwei Faelle ab:
///   1. Gescannte PDFs (pures Bild, kein Text-Layer)
///   2. IKAS-Caesar-PDFs mit Custom-Font, bei denen Zahlen im Byte-Shift verloren gehen
/// Render-Pipeline: Docnet.Core (PDFium) → BGRA-Bitmap → Windows.Media.Ocr (deutsche Sprache).
/// Benoetigt Win10 1903+ und installiertes deutsches Sprachpaket (normalerweise vorhanden).
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class OcrPdfFallbackService
{
    private const int RenderWidth = 1700;
    private const int RenderHeight = 2340;

    // Docnet-Aufrufe serialisieren — PDFium ist NICHT thread-safe und die
    // Library-Instanz ist ein global geteilter Singleton.
    private static readonly SemaphoreSlim _docnetGate = new(1, 1);

    /// <summary>
    /// Rendert bis zu N Seiten des PDF via Docnet, laesst Windows-OCR drueberlaufen.
    /// Gibt den zusammengesetzten Text zurueck. Bei Fehler: leerer String.
    /// </summary>
    public static async Task<string> ExtractTextAsync(
        string pdfPath,
        int maxPages = 5,
        CancellationToken ct = default)
    {
        if (!File.Exists(pdfPath)) return string.Empty;

        OcrEngine? engine = OcrEngine.TryCreateFromLanguage(new Language("de"))
                         ?? OcrEngine.TryCreateFromLanguage(new Language("de-CH"))
                         ?? OcrEngine.TryCreateFromLanguage(new Language("de-DE"))
                         ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine == null) return string.Empty;

        // 1) Bilder synchron aus dem PDF ziehen (PDFium thread-unsafe, keine awaits innen)
        List<(byte[] Bgra, int Width, int Height)> pages;
        try
        {
            pages = ExtractPageBitmaps(pdfPath, maxPages);
        }
        catch
        {
            return string.Empty;
        }
        if (pages.Count == 0) return string.Empty;

        // 2) OCR asynchron laufen lassen — PDFium-Handles sind da bereits freigegeben
        var sb = new StringBuilder();
        foreach (var (bgra, w, h) in pages)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                string pageText = await OcrBitmapAsync(engine, bgra, w, h).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine(pageText);
                    sb.AppendLine();
                }
            }
            catch
            {
                // Einzelne Seite uebergehen, weitere probieren
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Synchrone PDF→Bitmap-Extraktion unter Semaphore-Schutz.
    /// DocLib.Instance ist Singleton und darf NICHT mit 'using' disposed werden.
    /// </summary>
    private static List<(byte[] Bgra, int Width, int Height)> ExtractPageBitmaps(
        string pdfPath, int maxPages)
    {
        var result = new List<(byte[], int, int)>();

        // Blockierend warten — OCR-Pfad laeuft ohnehin selten und pro PDF nur einmal.
        _docnetGate.Wait();
        try
        {
            var library = DocLib.Instance; // KEIN using — globaler Singleton
            using var docReader = library.GetDocReader(
                pdfPath, new PageDimensions(RenderWidth, RenderHeight));

            int pageCount = Math.Min(docReader.GetPageCount(), maxPages);
            for (int i = 0; i < pageCount; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                var bgra = pageReader.GetImage();
                int width = pageReader.GetPageWidth();
                int height = pageReader.GetPageHeight();
                if (bgra != null && width > 0 && height > 0)
                    result.Add((bgra, width, height));
            }
        }
        finally
        {
            _docnetGate.Release();
        }
        return result;
    }

    private static async Task<string> OcrBitmapAsync(OcrEngine engine, byte[] bgra, int width, int height)
    {
        var buffer = bgra.AsBuffer();
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            buffer, BitmapPixelFormat.Bgra8, width, height);
        var result = await engine.RecognizeAsync(bitmap);
        if (result == null) return string.Empty;

        // result.Text wirft alle Zeilen mit Spaces zusammen — der Protokoll-Parser
        // arbeitet aber zeilenbasiert. result.Lines liefert die OCR-Zeilen einzeln.
        var sb = new StringBuilder();
        foreach (var line in result.Lines)
            sb.AppendLine(line.Text);
        return sb.ToString();
    }
}
