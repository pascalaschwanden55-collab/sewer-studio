using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

/// <summary>
/// Slice 1: baut aus einem Haltungsordner (Video + PDF) eine
/// <see cref="OperateurAnnotationSession"/>. Sucht das erste Video und
/// das erste PDF im Ordner, extrahiert den Volltext via
/// <see cref="PdfTextExtractor"/> und delegiert an <see cref="BeobachtungParser"/>.
///
/// Bewusst Klassifizierungs-arm gehalten: wir uebernehmen den Code
/// 1:1 aus dem PDF, das Mapping zu YOLO-Class-IDs passiert erst beim
/// Commit (VsaYoloClassMap.TryGetClassId). Unbekannte Codes scheitern
/// dann scharf — nicht hier beim Import.
/// </summary>
public sealed class OperateurSessionBuilder
{
    private static readonly string[] VideoExtensions =
        new[] { ".mp4", ".mov", ".m4v", ".mpg", ".mpeg", ".avi", ".mkv", ".asf", ".wmv" };

    private readonly string? _explicitPdfToTextPath;

    public OperateurSessionBuilder(string? explicitPdfToTextPath = null)
    {
        _explicitPdfToTextPath = explicitPdfToTextPath;
    }

    /// <summary>
    /// Pure-Text-Variante fuer Tests: PDF-Volltext + Pfade explizit
    /// uebergeben. Liefert eine Session mit allen Codes als Pending-Tasks.
    /// </summary>
    public OperateurAnnotationSession BuildFromText(
        string pdfText,
        string videoPath,
        string pdfPath,
        string caseId)
    {
        if (caseId is null) throw new ArgumentNullException(nameof(caseId));
        if (videoPath is null) throw new ArgumentNullException(nameof(videoPath));
        if (pdfPath is null) throw new ArgumentNullException(nameof(pdfPath));

        var session = new OperateurAnnotationSession
        {
            CaseId = caseId,
            VideoPath = videoPath,
            PdfPath = pdfPath,
            StartedUtc = DateTime.UtcNow,
        };

        var beobachtungen = BeobachtungParser.Parse(pdfText ?? "");
        // Sortieren nach Meterstand, damit der Operator den Codes
        // chronologisch durch das Video folgen kann.
        foreach (var b in beobachtungen.OrderBy(b => b.Meter))
        {
            session.Tasks.Add(new CodeTask
            {
                Code = b.Code,
                Meterstand = b.Meter,
            });
        }
        return session;
    }

    /// <summary>
    /// Folder-Variante: findet Video + PDF im Ordner, extrahiert Volltext
    /// via pdftotext, baut die Session. Wirft, wenn Video oder PDF fehlt.
    /// </summary>
    public OperateurAnnotationSession BuildFromFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("folderPath Pflicht.", nameof(folderPath));
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Haltungsordner nicht gefunden: {folderPath}");

        var videoPath = FindFirstVideo(folderPath)
            ?? throw new FileNotFoundException(
                $"Kein Video im Haltungsordner gefunden ({folderPath}). " +
                "Erlaubte Formate: " + string.Join(", ", VideoExtensions));
        var pdfPath = FindFirstPdf(folderPath)
            ?? throw new FileNotFoundException(
                $"Kein PDF im Haltungsordner gefunden ({folderPath}).");

        var extraction = PdfTextExtractor.ExtractPages(pdfPath, _explicitPdfToTextPath);
        var caseId = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));

        return BuildFromText(extraction.FullText, videoPath, pdfPath, caseId);
    }

    private static string? FindFirstVideo(string folderPath)
    {
        var set = new HashSet<string>(VideoExtensions, StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(folderPath)
            .Where(f => set.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? FindFirstPdf(string folderPath)
        => Directory.EnumerateFiles(folderPath, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
}
