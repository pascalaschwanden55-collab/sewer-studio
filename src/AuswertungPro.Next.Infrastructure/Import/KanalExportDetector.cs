using System;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Erkanntes Format eines Kanal-TV-Exportordners.
/// </summary>
public enum KanalExportFormat
{
    /// <summary>Format konnte nicht bestimmt werden.</summary>
    Unknown,

    /// <summary>IKAS/IBAK-Export (VSA_KEK-XTF und/oder KiasExportPattern erkannt).</summary>
    Ikas,

    /// <summary>IBAK/KIAS-Export (Arizona.fdb + Film/Daten.txt oder Report-PDFs, ohne VSA_KEK-XTF).</summary>
    Ibak,

    /// <summary>WinCan-Export (*.db3 in einem DB-Unterordner gefunden).</summary>
    WinCan,

    /// <summary>Sowohl WinCan- als auch IKAS-Signale gefunden — mehrdeutig.</summary>
    Ambiguous,

    /// <summary>KINS-DVD-Export (kiDVDaten.txt gefunden; VSAKEK-XTF optional dabei).</summary>
    Kins
}

/// <summary>
/// Ergebnis der Format-Erkennung eines Exportordners.
/// </summary>
public sealed record KanalExportDetection(
    KanalExportFormat Format,
    string?           Db3Path,        // WinCan: groesste .db3 unter \DB\
    string?           VsaKekXtfPath,  // IKAS/KINS: VSA_KEK-XTF
    string?           Sia405XtfPath,  // IKAS: SIA405-XTF (optional)
    string?           Reason,
    string?           KinsDataTxtPath = null, // KINS: kiDVDaten.txt
    string?           ViewerMdbPath = null);  // WinCan v8: DB\*_Viewer.mdb (kein .db3)

/// <summary>
/// Erkennt das Format eines Kanal-TV-Exportordners (WinCan vs. IKAS)
/// und liefert die Fundorte der Nutzdateien.
///
/// WinCan-Kriterium:  *.db3 in einem Unterordner namens "DB" (rekursiv);
///                    *_Meta.db3 werden ignoriert; bei mehreren nimmt man die groesste.
///
/// IKAS-Kriterium:    IKiasExportPatternDetector.Detect(root).IsKias == true
///                    ODER eine .xtf-Datei enthaelt "VSA_KEK_2020_LV95".
///
/// Treffen beide zu → Ambiguous; keins → Unknown.
/// </summary>
public sealed class KanalExportDetectionService : IKanalExportDetectionService
{
    // Anzahl Bytes die beim XTF-Header-Scan gelesen werden (erste ~64 KB reichen)
    private const int XtfHeaderBytes = 65_536;
    private readonly IKiasExportPatternDetector _kiasExportPatternDetector;

    public KanalExportDetectionService()
        : this(KiasExportPattern.Current)
    {
    }

    public KanalExportDetectionService(IKiasExportPatternDetector kiasExportPatternDetector)
    {
        _kiasExportPatternDetector = kiasExportPatternDetector
            ?? throw new ArgumentNullException(nameof(kiasExportPatternDetector));
    }

    /// <summary>
    /// Analysiert <paramref name="sourceFolder"/> und liefert das erkannte Format
    /// samt Pfaden zu den relevanten Nutzdateien.
    /// </summary>
    public KanalExportDetection Detect(string sourceFolder)
    {
        // Ungueltige oder nicht vorhandene Pfade → Unknown
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return new KanalExportDetection(
                KanalExportFormat.Unknown, null, null, null,
                "Pfad nicht vorhanden oder leer");

        // --- KINS-Suche (kiDVDaten.txt = eindeutiger DVD-Marker) ---
        var kinsTxtPath = FindKinsDataTxt(sourceFolder);

        // --- WinCan-Suche ---
        var db3Path = FindWinCanDb3(sourceFolder);
        // Alte WinCan-v8-Exporte fuehren die Daten als Access-Datei unter DB\.
        // Ohne diesen Zweig meldete die Erkennung "Unknown" und der Ein-Knopf-Import
        // brach ab - obwohl der vorhandene MDB-Rueckfall die Datei lesen kann.
        var viewerMdbPath = db3Path is null ? FindWinCanViewerMdb(sourceFolder) : null;
        var hatWinCanQuelle = db3Path is not null || viewerMdbPath is not null;

        // --- IKAS-Suche ---
        var (vsaKekPath, sia405Path, vsaKekAnyPath) = FindXtfFiles(sourceFolder);
        var isIkasByXtf = vsaKekPath is not null;
        var isIbakByPattern = _kiasExportPatternDetector.Detect(sourceFolder).IsKias;
        var isIkasOrIbak = isIkasByXtf || isIbakByPattern;

        // --- Format bestimmen ---
        KanalExportFormat format;
        string reason;

        // KINS gewinnt vor IKAS: KINS-Exporte enthalten oft ein VSAKEK-XTF,
        // dessen Header auch die SIA405-Modelle listet — ohne diesen Vorrang
        // liefe der Ordner faelschlich als IKAS/Unknown.
        if (kinsTxtPath is not null && hatWinCanQuelle)
        {
            return new KanalExportDetection(
                KanalExportFormat.Ambiguous, db3Path, vsaKekPath ?? vsaKekAnyPath, sia405Path,
                "Sowohl KINS (kiDVDaten.txt) als auch WinCan (Datenbank in DB/) vorhanden",
                kinsTxtPath,
                viewerMdbPath);
        }

        if (kinsTxtPath is not null)
        {
            // Fuer KINS zaehlt jedes XTF mit VSA_KEK-Modell — auch wenn der
            // Header zusaetzlich SIA405-Modelle referenziert.
            var kinsXtf = vsaKekPath ?? vsaKekAnyPath;
            return new KanalExportDetection(
                KanalExportFormat.Kins, db3Path, kinsXtf, sia405Path,
                kinsXtf is not null
                    ? $"KINS erkannt: kiDVDaten.txt + VSA_KEK-XTF {Path.GetFileName(kinsXtf)}"
                    : "KINS erkannt: kiDVDaten.txt (kein VSAKEK-XTF)",
                kinsTxtPath,
                viewerMdbPath);
        }

        if (hatWinCanQuelle && isIkasOrIbak)
        {
            format = KanalExportFormat.Ambiguous;
            reason = "Sowohl WinCan (Datenbank in DB/) als auch IKAS/IBAK-Signale vorhanden";
        }
        else if (db3Path is not null)
        {
            format = KanalExportFormat.WinCan;
            reason = $"WinCan erkannt: {Path.GetFileName(db3Path)}";
        }
        else if (viewerMdbPath is not null)
        {
            format = KanalExportFormat.WinCan;
            reason = $"WinCan erkannt: Viewer-Datenbank {Path.GetFileName(viewerMdbPath)} (mdb, kein .db3)";
        }
        else if (isIkasByXtf)
        {
            format = KanalExportFormat.Ikas;
            reason = $"IKAS erkannt: VSA_KEK-XTF {Path.GetFileName(vsaKekPath)}";
        }
        else if (isIbakByPattern)
        {
            format = KanalExportFormat.Ibak;
            reason = "IBAK/KIAS erkannt: KiasExportPattern";
        }
        else
        {
            format = KanalExportFormat.Unknown;
            reason = "Kein WinCan (.db3/.mdb in DB/) und kein IKAS/IBAK-Signal gefunden";
        }

        return new KanalExportDetection(
            format, db3Path, vsaKekPath, sia405Path, reason, null, viewerMdbPath);
    }

    // -------------------------------------------------------------------------
    // WinCan: *.db3 in einem Unterordner namens "DB" suchen
    // -------------------------------------------------------------------------

    /// <summary>
    /// Waehlt dieselbe Datei wie der Import.
    ///
    /// Frueher stand die Regel hier ein zweites Mal und lief mit der Kopie im
    /// WinCanDbImportService auseinander: Diese Stelle schloss "*_Meta.db3" aus, die
    /// andere nicht. Die Erkennung meldete die richtige Datei, der Importer oeffnete
    /// eine andere und las null Haltungen. Beide gehen jetzt ueber
    /// <see cref="WinCanDb3Pruefer"/> — es gibt keine zweite Regel mehr.
    /// </summary>
    private static string? FindWinCanDb3(string root)
        => Quellenwahl.Waehle(
            WinCan.WinCanDb3Pruefer.FindeKandidaten(root),
            WinCan.WinCanDb3Pruefer.Pruefe).BesterErkannter?.Pfad;

    /// <summary>
    /// Sucht die groesste Access-Datei unter einem Ordner "DB". Die Einschraenkung auf
    /// DB\ ist wichtig: sonst wuerde irgendeine Access-Datei im Kundenordner den Import
    /// auf WinCan umlenken.
    /// </summary>
    private static string? FindWinCanViewerMdb(string root)
    {
        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible    = true,
                AttributesToSkip      = FileAttributes.None,
                MatchCasing           = MatchCasing.CaseInsensitive
            };

            string? best     = null;
            long    bestSize = -1;

            foreach (var path in Directory.EnumerateFiles(root, "*.mdb", opts))
            {
                if (!IsUnderDbFolder(path))
                    continue;

                try
                {
                    var size = new FileInfo(path).Length;
                    if (size > bestSize)
                    {
                        bestSize = size;
                        best     = path;
                    }
                }
                catch { /* nicht zugreifbar -> ueberspringen */ }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Prueft, ob <paramref name="filePath"/> in einem Ordner namens "DB" liegt
    /// (unmittelbarer Parent-Ordner muss "DB" heissen).
    /// </summary>
    private static bool IsUnderDbFolder(string filePath)
    {
        var parent = Path.GetDirectoryName(filePath);
        if (parent is null) return false;
        return string.Equals(
            Path.GetFileName(parent),
            "DB",
            StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // IKAS: VSA_KEK-XTF und SIA405-XTF suchen
    // -------------------------------------------------------------------------

    private static (string? vsaKekPath, string? sia405Path, string? vsaKekAnyPath) FindXtfFiles(string root)
    {
        string? vsaKekPath = null;
        string? sia405Path = null;
        string? vsaKekAnyPath = null; // VSA_KEK-Modell vorhanden, egal ob auch SIA405 im Header (KINS-Fall)

        foreach (var path in SafeFileEnumeration.EnumerateFilesSafe(root, "*", recursive: true))
        {
            if (!Path.GetExtension(path).Equals(".xtf", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = ReadXtfHeader(path);
            if (content is null) continue;

            if (content.Contains("VSA_KEK_2020_LV95", StringComparison.OrdinalIgnoreCase))
                vsaKekAnyPath ??= path;

            // SIA405-XTF: Inhalt enthaelt "SIA405"
            if (content.Contains("SIA405", StringComparison.OrdinalIgnoreCase))
            {
                sia405Path ??= path;
                continue; // SIA405-Dateien koennen auch VSA_KEK enthalten → bevorzuge reines VSA_KEK
            }

            // VSA_KEK-XTF: Inhalt enthaelt "VSA_KEK_2020_LV95" und kein SIA405-Dateiname
            if (content.Contains("VSA_KEK_2020_LV95", StringComparison.OrdinalIgnoreCase))
                vsaKekPath ??= path;
        }

        return (vsaKekPath, sia405Path, vsaKekAnyPath);
    }

    // -------------------------------------------------------------------------
    // KINS: kiDVDaten.txt suchen (Marker der Kanal-Info-DVD)
    // -------------------------------------------------------------------------

    private static string? FindKinsDataTxt(string root)
    {
        foreach (var path in SafeFileEnumeration.EnumerateFilesSafe(root, "*", recursive: true))
        {
            if (Path.GetFileName(path).Equals("kiDVDaten.txt", StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Liest die ersten <see cref="XtfHeaderBytes"/> einer Datei als UTF-8-Text.
    /// Gibt null zurueck wenn die Datei nicht lesbar ist.
    /// </summary>
    private static string? ReadXtfHeader(string path)
    {
        try
        {
            using var fs  = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buf       = new byte[Math.Min(XtfHeaderBytes, fs.Length)];
            var read      = fs.Read(buf, 0, buf.Length);
            return Encoding.UTF8.GetString(buf, 0, read);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer der Formaterkennung.
/// </summary>
public static class KanalExportDetector
{
    private static readonly IKanalExportDetectionService DefaultDetector =
        new KanalExportDetectionService();

    public static KanalExportDetection Detect(string sourceFolder)
        => DefaultDetector.Detect(sourceFolder);
}
