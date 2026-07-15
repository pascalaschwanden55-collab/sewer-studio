using AuswertungPro.Next.Application.Import;

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
        var hasFdb = HasFile(exportRoot, "Arizona.fdb")
                     || (Directory.Exists(dataDirectory) && HasFile(dataDirectory, "*.fdb"));

        var filmDirectory = Path.Combine(exportRoot, "Film");
        var hasFilm = Directory.Exists(filmDirectory);
        var reportDirectory = Path.Combine(exportRoot, "Report");
        var hasReport = Directory.Exists(reportDirectory);
        var hasDatenTxt = hasFilm && File.Exists(Path.Combine(filmDirectory, "Daten.txt"));

        var holdingPdfs = 0;
        var lateralPdfs = 0;
        if (hasReport)
        {
            try
            {
                holdingPdfs = Directory.EnumerateFiles(reportDirectory, "H_*.pdf").Count();
                lateralPdfs = Directory.EnumerateFiles(reportDirectory, "L_*.pdf").Count();
            }
            catch
            {
                // Einzelne nicht lesbare Report-Ordner verhindern die Formaterkennung nicht.
            }
        }

        var gegenrichtung = 0;
        var wiederholung = 0;
        if (hasFilm)
        {
            try
            {
                foreach (var filePath in Directory.EnumerateFiles(filmDirectory, "*.*"))
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
            catch
            {
                // Einzelne nicht lesbare Film-Ordner verhindern die Formaterkennung nicht.
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

    private static bool HasFile(string directory, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(
                    directory,
                    pattern,
                    SearchOption.TopDirectoryOnly)
                .Any();
        }
        catch
        {
            return false;
        }
    }
}
