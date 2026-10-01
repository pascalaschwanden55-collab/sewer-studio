using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Erkennt KIAS-/IBAK-Exportordner anhand von Arizona.fdb, Film und Report.
/// </summary>
public sealed class KiasExportPatternDetectionService : IKiasExportPatternDetector
{
    public KiasExportDetectionResult Detect(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
        {
            return new KiasExportDetectionResult(
                false, false, false, false, false, 0, 0, 0, 0,
                "Pfad nicht vorhanden");
        }

        var dataDirectory = Path.Combine(exportRoot, "Data");
        var hasData = IsSafeStandardDirectory(dataDirectory);
        var hasFdb = HasFile(exportRoot, "Arizona.fdb")
                     || (hasData && HasFile(dataDirectory, "*.fdb", requireSafeDirectory: true));

        var filmDirectory = Path.Combine(exportRoot, "Film");
        var hasFilm = IsSafeStandardDirectory(filmDirectory);
        var reportDirectory = Path.Combine(exportRoot, "Report");
        var hasReport = IsSafeStandardDirectory(reportDirectory);
        var hasDatenTxt = hasFilm
                          && HasFile(filmDirectory, "Daten.txt", requireSafeDirectory: true);

        var holdingPdfs = 0;
        var lateralPdfs = 0;
        if (hasReport)
        {
            holdingPdfs = EnumerateDirectFiles(
                    reportDirectory,
                    "H_*.pdf",
                    requireSafeDirectory: true)
                .Count;
            lateralPdfs = EnumerateDirectFiles(
                    reportDirectory,
                    "L_*.pdf",
                    requireSafeDirectory: true)
                .Count;
        }

        var gegenrichtung = 0;
        var wiederholung = 0;
        if (hasFilm)
        {
            foreach (var filePath in EnumerateDirectFiles(
                         filmDirectory,
                         "*.*",
                         requireSafeDirectory: true))
            {
                var name = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (KiasExportPattern.IsGegenrichtungName(name))
                    gegenrichtung++;
                else if (KiasExportPattern.HasTildeSuffix(name))
                    wiederholung++;
            }
        }

        var hasReportPdfs = holdingPdfs + lateralPdfs > 0;
        var isKias = hasFdb && hasFilm && (hasDatenTxt || hasReportPdfs);
        var reason = isKias
            ? $"KIAS erkannt: Arizona.fdb + Film/ + {(hasDatenTxt ? "Daten.txt" : "Report-PDFs")}"
            : $"Kein KIAS: hasFdb={hasFdb}, hasFilm={hasFilm}, hasDatenTxt={hasDatenTxt}, reportPdfs={holdingPdfs + lateralPdfs}";

        return new KiasExportDetectionResult(
            IsKias: isKias,
            HasArizonaFdb: hasFdb,
            HasFilmFolder: hasFilm,
            HasReportFolder: hasReport,
            HasDatenTxt: hasDatenTxt,
            HoldingPdfCount: holdingPdfs,
            LateralPdfCount: lateralPdfs,
            GegenrichtungVideoCount: gegenrichtung,
            RepeatTakeVideoCount: wiederholung,
            Reason: reason);
    }

    private static bool HasFile(
        string directory,
        string pattern,
        bool requireSafeDirectory = false)
        => EnumerateDirectFiles(directory, pattern, requireSafeDirectory).Count > 0;

    private static IReadOnlyList<string> EnumerateDirectFiles(
        string directory,
        string pattern,
        bool requireSafeDirectory)
    {
        // Der ausdruecklich gewaehlte Export-Root darf selbst eine Verknuepfung sein.
        // Seine bekannten Unterordner muessen dagegen echte Ordner sein.
        if (requireSafeDirectory && !IsSafeStandardDirectory(directory))
            return Array.Empty<string>();

        var files = SafeFileEnumeration
            .EnumerateFilesSafe(directory, pattern, recursive: false)
            .ToArray();

        // Zweite Pruefung nach dem Aufzaehlen: Ein waehrenddessen ausgetauschter
        // Standardordner darf keine bereits gefundenen Fremddateien liefern.
        return !requireSafeDirectory || IsSafeStandardDirectory(directory)
            ? files
            : Array.Empty<string>();
    }

    private static bool IsSafeStandardDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        try
        {
            ImportFileStagingPathGuard.EnsureNotReparsePoint(directory);
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or System.Security.SecurityException)
        {
            return false;
        }
    }
}
