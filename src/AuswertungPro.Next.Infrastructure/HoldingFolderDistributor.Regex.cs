using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Regex-Patterns + String-Konstanten fuer HoldingFolderDistributor.
///
/// Refactor 2026-05-07 (Etappe 1, Charge R1): mechanisch ausgegliedert
/// aus der Hauptdatei. 17 Regex-Patterns + SchachtIdPat-const +
/// VideoExtensionPattern. Keine Verhaltensaenderung — alle Patterns sind
/// statische Felder einer partial class und damit identisch zur vorherigen
/// Anordnung.
///
/// Charakterisierungs-Tests (22) bleiben gruen.
/// </summary>
public static partial class HoldingFolderDistributor
{
    // ── KINS-TXT (Daten.txt-Format) ────────────────────────────────────────
    private static readonly Regex KinsTxtHeaderRegex = new(
        @"^\s*(?<usage>\S+)\s+(?<from>[0-9.]+)\s*->\s*(?<to>[0-9.]+).*?@Datei=(?<video>[^\s]+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex KinsTxtDateRegex = new(
        @"(?<d>\d{2}\.\d{2}\.\d{2,4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ── Video-Extensions (zur Laufzeit aus MediaFileTypes zusammengesetzt) ─
    private static readonly string VideoExtensionPattern =
        string.Join("|", MediaFileTypes.VideoExtensions.Select(ext => Regex.Escape(ext.TrimStart('.'))));

    // ── PDF-Header & Filename-Pairs ────────────────────────────────────────
    private static readonly Regex PdfHeaderRegex = new(
        @"Haltungs(?:\s*inspektion|bilder)\s*[-–—]\s*(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})\s*[-–—]\s*((?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FilmNameRegex = new(
        $@"Film(?:name|datei)?\s*[:\-]?\s*([A-Za-z0-9_\-\. ]+?\.(?:{VideoExtensionPattern}))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PdfFilenamePairRegex = new(
        @"(?:\d{2,}\.\d{2,}|\d{4,})\s*[-_]\s*(?:\d{2,}\.\d{2,}|\d{4,})",
        RegexOptions.Compiled);

    // ── Hotpath-Regex: TryExtractDichtheitShafts ───────────────────────────
    // Schacht-ID-Pattern: numerisch (81150, 42.046) oder alphanumerisch (S42.123, KS-0815)
    private const string SchachtIdPat = @"[A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?";
    private static readonly Regex DichtheitUpperRx = new(
        @"oberer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DichtheitLowerRx = new(
        @"unterer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SchachtObenRx = new(
        @"Schacht\s*oben\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SchachtUntenRx = new(
        @"Schacht\s*unten\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Hotpath-Regex: TryFindInspectionDate / TryFindSchachtDate / TryExtractDateFromFormEntries ─
    private static readonly Regex InspectionDateRx = new(
        @"(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);
    private static readonly Regex FormEntryDateRx = new(
        @"\b(?<d>\d{2}[./-]\d{2}[./-]\d{2,4}|\d{4}[./-]\d{2}[./-]\d{2})\b",
        RegexOptions.Compiled);
    private static readonly Regex LabeledDateRx = new(
        @"Datum\s*[:\-]?\s*(?<date>\d{2}[./-]\d{2}[./-]\d{2,4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GenericDateRx = new(
        @"\b(?<date>\d{2}[./-]\d{2}[./-]\d{2,4})\b",
        RegexOptions.Compiled);

    // ── Hotpath-Regex: TryFindHaltungId ────────────────────────────────────
    private static readonly Regex HaltungIdRx = new(
        @"(?im)^.*Haltung.*[:\-\s]+(?<id>[\d\.\- ]{5,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GeneralPairRx = new(
        @"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))(?=[^\d]|$)",
        RegexOptions.Compiled);
    private static readonly Regex GluedDatePairRx = new(
        @"((?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}?))(?=\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);
    private static readonly Regex ConcatenatedIdRx = new(
        @"(?:Haltungsname|Schacht\s*oben|Schacht\s*unten|Oberer\s*Punkt|Unterer\s*Punkt).{0,300}?(?<id>\d{10})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
}
