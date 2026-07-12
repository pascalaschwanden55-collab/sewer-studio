using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

// Text-, Datums-, Schluessel- und Pfad-Helfer.
// Teil derselben partial-Klasse - reine mechanische Auslagerung (kein Verhaltenswechsel).
public static partial class HoldingFolderDistributor
{

    private static string NormalizeText(string text) => HoldingTextNormalizer.NormalizeText(text);


    private static bool TryParseDateString(string value, out DateTime date) => HoldingTextNormalizer.TryParseDateString(value, out date);


    private static bool IsSuspiciousShaftPair(string shaftPair, string explicitPair)
        => HoldingDistribution.HoldingTextParser.IsSuspiciousShaftPair(shaftPair, explicitPair);


    private static string? MergeMessage(string? a, string? b) => HoldingTextNormalizer.MergeMessage(a, b);


    private static string? TryExtractHaltungFromPdfPath(string? pdfPath)
        => HoldingDistribution.HoldingTextParser.TryExtractHaltungFromPdfPath(pdfPath);


    private static string NormalizeShaftNumberKey(string? value)
        => HoldingDistribution.HoldingTextParser.NormalizeShaftNumberKey(value);


    private static string BuildPageRange(IReadOnlyList<int> pages) => HoldingTextNormalizer.BuildPageRange(pages);


    private static bool IsContentsPage(string text) => HoldingTextNormalizer.IsContentsPage(text);


    private static DateTime? TryFindInspectionDate(string text)
        => HoldingDistribution.HoldingTextParser.TryFindInspectionDate(text);


    private static DateTime? TryFindSchachtDate(string text)
        => HoldingDistribution.HoldingTextParser.TryFindSchachtDate(text);


    private static DateTime? FindNearbyDate(string[] lines, int startIndex, int step, int maxLines, Regex dateRx)
        => HoldingDistribution.HoldingTextParser.FindNearbyDate(lines, startIndex, step, maxLines, dateRx);


    private static string? TryFindHaltungId(string text)
        => HoldingDistribution.HoldingTextParser.TryFindHaltungId(text);


    private static string? TryParseKsCompactHoldingDigits(string rawDigits)
        => HoldingDistribution.HoldingTextParser.TryParseKsCompactHoldingDigits(rawDigits);


    private static string? TryFindSchachtNumber(string text)
        => HoldingDistribution.HoldingTextParser.TryFindSchachtNumber(text);

    // Schacht-Wert: numerisch (81150, 42.046) ODER alphanumerisch (S42.123, KS-0815, A1-B2)


    private static IReadOnlyList<string> Tokenize(string line)
        => line.Split(new[] { ' ', '\t', ';', ',', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();


    private static bool HasVideoExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        return MediaFileTypes.HasVideoExtension(normalized);
    }


    private static bool HasImageExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        return MediaFileTypes.HasImageExtension(normalized);
    }


    private static string? NormalizeVideoFileName(string? value) => HoldingTextNormalizer.NormalizeVideoFileName(value);


    private static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);


    private static string NormalizeHaltungId(string? value) => HoldingIdNormalizer.NormalizeHaltungId(value);


    private static string NormalizeKey(string value) => HoldingTextNormalizer.NormalizeKey(value);


    private static bool IsValidHaltungId(string? value) => HoldingIdNormalizer.IsValidHaltungId(value);

    /// <summary>
    /// Prueft ob im Haltungsordner bereits ein Video mit gleicher Dateigroesse existiert.
    /// Gibt den Pfad zurueck wenn ja, sonst null.
    /// Verhindert Duplikate beim erneuten Verteilen.
    /// </summary>

    internal static string MakeProjectRelativeLink(string mediaPath, string municipalityFolder)
    {
        var current = new DirectoryInfo(Path.GetFullPath(municipalityFolder));
        while (current is not null)
        {
            if (string.Equals(current.Name, "Haltungen", StringComparison.OrdinalIgnoreCase)
                && current.Parent is not null)
            {
                return ProjectPathResolver.MakeRelative(mediaPath, current.Parent.FullName);
            }

            current = current.Parent;
        }

        // Unbekannte Altstruktur: absoluten Pfad behalten, statt einen falsch aufgeloesten
        // relativen Link zu erzeugen.
        return mediaPath;
    }


}
