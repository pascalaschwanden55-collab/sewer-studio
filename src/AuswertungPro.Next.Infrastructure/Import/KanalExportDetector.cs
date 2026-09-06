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
    string?           ViewerMdbPath = null)   // WinCan v8: DB\*_Viewer.mdb (kein .db3)
{
    /// <summary>
    /// Jede geprueste Fachquelle des Ordners mit Typ und Auswahl- bzw. Ablehnungsgrund.
    ///
    /// Additiv seit 2026-09-05: Vorher meldete die Erkennung nur "Unknown" und ein
    /// pauschales "kein Signal gefunden". Wer wissen wollte, WAS im Ordner liegt und
    /// warum es nicht genommen wurde, musste den Ordner selbst durchsuchen.
    /// </summary>
    public IReadOnlyList<KanalQuellenkandidat> Kandidaten { get; init; } = Array.Empty<KanalQuellenkandidat>();

    /// <summary>WinCan-VX-Datenbank (SQL Server Compact). Nicht direkt lesbar — braucht den XTF-Ersatzweg.</summary>
    public string? SdfPath { get; init; }

    /// <summary>
    /// Die XTF-Dateien mit verwertbarem Fachinhalt, nach Abzug doppelter Exporte.
    ///
    /// Ausdruecklich unabhaengig davon, ob der gewaehlte Importweg sie verwendet:
    /// Ein IBAK-Ordner wird ueber sein Dateimuster erkannt und liest keine XTF — die
    /// danebenliegende Inspektions-XTF traegt in Goeschenen Unterdorfstrasse trotzdem
    /// 71 Schaechte und 86 Videolinks, die sonst niemand sieht (gemessen 2026-09-05).
    /// </summary>
    public IReadOnlyList<string> FachlicheXtfQuellen { get; init; } = Array.Empty<string>();
}

/// <summary>Art einer im Exportordner gefundenen Fachquelle.</summary>
public enum KanalQuellenart
{
    Datenbank,
    Xtf,
    Textdatei
}

/// <summary>
/// Eine gefundene Fachquelle samt Entscheid. <paramref name="Uebernommen"/> false heisst
/// nicht "kaputt" — es kann auch schlicht die schwaechere von zwei gleichwertigen Quellen sein.
/// </summary>
public sealed record KanalQuellenkandidat(
    string Pfad,
    KanalQuellenart Art,
    string Typ,
    bool Uebernommen,
    string Grund);

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
    private readonly IKiasExportPatternDetector _kiasExportPatternDetector;
    private readonly IXtfQuellenPruefer _xtfPruefer;

    // Ergebnisse je Wurzel, damit jede XTF-Datei pro Erkennungslauf nur einmal
    // gelesen wird. Der Dienst wird pro Import neu erzeugt; der Zwischenspeicher
    // ueberlebt also keinen zweiten Lauf.
    private readonly Dictionary<string, List<(string Pfad, XtfQuellenmerkmale Merkmale)>> _xtfPruefungCache
        = new(StringComparer.OrdinalIgnoreCase);

    // Erkennt bytegleiche XTF-Kopien — dieselbe Regel wie bei den Videos.
    private readonly IMedienInhaltsIndex _dateiInhalt = new Common.MedienInhaltsIndex();

    public KanalExportDetectionService()
        : this(KiasExportPattern.Current)
    {
    }

    public KanalExportDetectionService(IKiasExportPatternDetector kiasExportPatternDetector)
        : this(kiasExportPatternDetector, new Xtf.XtfQuellenPruefer())
    {
    }

    public KanalExportDetectionService(
        IKiasExportPatternDetector kiasExportPatternDetector,
        IXtfQuellenPruefer xtfPruefer)
    {
        _kiasExportPatternDetector = kiasExportPatternDetector
            ?? throw new ArgumentNullException(nameof(kiasExportPatternDetector));
        _xtfPruefer = xtfPruefer ?? throw new ArgumentNullException(nameof(xtfPruefer));
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
        // WinCan VX legt seine Daten als SQL-Compact-Datei ab. Sie ist unter .NET nicht
        // lesbar und macht den Ordner deshalb NOCH NICHT importierbar — sie gehoert aber
        // in die Kandidatenliste, damit der Benutzer sieht, was gefunden wurde.
        var sdfPath = hatWinCanQuelle ? null : FindWinCanSdf(sourceFolder);

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
                viewerMdbPath)
            {
                SdfPath = sdfPath,
                FachlicheXtfQuellen = FachlicheXtf(sourceFolder),
                Kandidaten = BaueKandidatenliste(
                    sourceFolder, db3Path, viewerMdbPath, sdfPath, kinsTxtPath, vsaKekPath ?? vsaKekAnyPath)
            };
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
                viewerMdbPath)
            {
                SdfPath = sdfPath,
                FachlicheXtfQuellen = FachlicheXtf(sourceFolder),
                Kandidaten = BaueKandidatenliste(
                    sourceFolder, db3Path, viewerMdbPath, sdfPath, kinsTxtPath, kinsXtf)
            };
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
            reason = BegruendeUnbekannt(sourceFolder, sdfPath);
        }

        return new KanalExportDetection(
            format, db3Path, vsaKekPath, sia405Path, reason, null, viewerMdbPath)
        {
            SdfPath = sdfPath,
            FachlicheXtfQuellen = FachlicheXtf(sourceFolder),
            Kandidaten = BaueKandidatenliste(
                sourceFolder, db3Path, viewerMdbPath, sdfPath, kinsTxtPath, vsaKekPath)
        };
    }

    /// <summary>
    /// Sagt beim unbekannten Ordner, WAS gefunden wurde. "Kein Signal gefunden" war die
    /// haeufigste und unbrauchbarste Meldung des Ein-Knopf-Imports: Bei 17 von 52
    /// geprueften Ordnern lagen sehr wohl Fachdaten, nur in einer Form, die die
    /// Erkennung nicht kannte (Audit 2026-09-05).
    /// </summary>
    private string BegruendeUnbekannt(string root, string? sdfPath)
    {
        var teile = new List<string>();

        if (sdfPath is not null)
        {
            teile.Add(
                $"WinCan-VX-Datenbank {Path.GetFileName(sdfPath)} gefunden — SQL Server Compact, "
                + "unter .NET nicht direkt lesbar");
        }

        // Nur die tatsaechlich verwendbaren Exporte zaehlen. Sonst meldet der Grund bei
        // drei Exporten derselben Zone dreimal dieselben Untersuchungen — genau die
        // wertlose Zahl "144 gefunden" aus dem Audit.
        var kandidaten = PruefeXtfDateien(root)
            .Select(x => new XtfExportKandidat(x.Pfad, x.Merkmale))
            .ToList();
        var gewaehlt = XtfExportAuswahl.Waehle(kandidaten).Uebernommen
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var alteInspektionen = kandidaten
            .Where(x => gewaehlt.Contains(x.Pfad) && XtfQuellenklassifikation.IstInspektionsquelle(x.Merkmale))
            .Select(x => (x.Pfad, x.Merkmale))
            .ToList();

        if (alteInspektionen.Count > 0)
        {
            var untersuchungen = alteInspektionen.Sum(x => x.Merkmale.Untersuchungen);
            var modelle = alteInspektionen
                .SelectMany(x => x.Merkmale.Modellnamen)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            teile.Add(
                $"{alteInspektionen.Count} XTF-Datei(en) mit zusammen {untersuchungen} Untersuchungen "
                + $"(Modell {string.Join(", ", modelle)}) — dieses Modell ist fuer den Import noch nicht freigegeben");
        }

        // Eine unlesbare XTF darf nicht stillschweigend verschwinden: Sie IST eine
        // Fachquelle, sie liess sich nur nicht oeffnen.
        var unlesbar = kandidaten.Where(x => x.Merkmale.Lesefehler is not null).ToList();
        if (unlesbar.Count > 0)
        {
            teile.Add(
                $"{unlesbar.Count} XTF-Datei(en) nicht lesbar "
                + $"({string.Join(", ", unlesbar.Take(3).Select(x => Path.GetFileName(x.Pfad)))})");
        }

        return teile.Count == 0
            ? "Kein WinCan (.db3/.mdb in DB/) und kein IKAS/IBAK-Signal gefunden"
            : "Nicht importierbar, aber Fachdaten vorhanden: " + string.Join("; ", teile);
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

    private (string? vsaKekPath, string? sia405Path, string? vsaKekAnyPath) FindXtfFiles(string root)
    {
        string? vsaKekPath = null;
        string? sia405Path = null;
        string? vsaKekAnyPath = null; // VSA_KEK-Modell vorhanden, egal ob auch SIA405 im Header (KINS-Fall)

        foreach (var (path, merkmale) in PruefeXtfDateien(root))
        {
            if (merkmale.Lesefehler is not null)
                continue;

            var hatNeuesVsaKek = merkmale.Modellnamen.Any(
                m => m.StartsWith("VSA_KEK_2020_LV95", StringComparison.OrdinalIgnoreCase));
            var hatSia405 = merkmale.Modellnamen.Any(
                m => m.StartsWith("SIA405", StringComparison.OrdinalIgnoreCase));

            if (hatNeuesVsaKek)
                vsaKekAnyPath ??= path;

            // Eine Datei mit SIA405 im Kopf gilt als SIA405-Quelle; ein reines
            // VSA_KEK-2020-Dokument wird bevorzugt als IKAS-Hauptquelle genommen.
            if (hatSia405)
            {
                sia405Path ??= path;
                continue;
            }

            if (hatNeuesVsaKek)
                vsaKekPath ??= path;
        }

        return (vsaKekPath, sia405Path, vsaKekAnyPath);
    }

    /// <summary>
    /// Liest jede XTF des Ordners genau einmal und merkt sich das Ergebnis fuer diesen
    /// Erkennungslauf. Ohne diesen Zwischenspeicher wuerde dieselbe Datei fuer die
    /// Formatwahl und fuer die Kandidatenliste zweimal geparst.
    /// </summary>
    private List<(string Pfad, XtfQuellenmerkmale Merkmale)> PruefeXtfDateien(string root)
    {
        if (_xtfPruefungCache.TryGetValue(root, out var vorhanden))
            return vorhanden;

        var ergebnis = new List<(string, XtfQuellenmerkmale)>();
        foreach (var path in SafeFileEnumeration.EnumerateFilesSafe(root, "*", recursive: true))
        {
            if (!Path.GetExtension(path).Equals(".xtf", StringComparison.OrdinalIgnoreCase))
                continue;

            ergebnis.Add((path, _xtfPruefer.Pruefe(path)));
        }

        ergebnis.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
        _xtfPruefungCache[root] = ergebnis;
        return ergebnis;
    }

    /// <summary>
    /// Alle Fachquellen des Ordners mit Typ und Entscheid — die Grundlage fuer einen
    /// Importbericht, der sagt WAS gefunden wurde und WARUM es genommen oder abgelehnt wird.
    /// </summary>
    /// <summary>
    /// Die XTF-Dateien des Ordners mit Fachinhalt, doppelte Exporte zusammengefasst.
    /// </summary>
    private List<string> FachlicheXtf(string root)
    {
        var kandidaten = PruefeXtfDateien(root)
            .Select(x => new XtfExportKandidat(x.Pfad, x.Merkmale))
            .ToList();

        // Auch Katasterdateien: Sie tragen die Normschaechte.
        var gewaehlt = XtfExportAuswahl.Waehle(kandidaten, auchKataster: true).Uebernommen;

        // Katasterdateien haben keine Untersuchungen und damit keinen Fingerabdruck —
        // zwei identische Kopien wuerden sonst beide gelesen. Der Inhaltsvergleich aus
        // der Medien-Erkennung fasst sie zusammen.
        var jeInhalt = new Dictionary<string, string>(StringComparer.Ordinal);
        var ergebnis = new List<string>();
        foreach (var kandidat in _dateiInhalt.Pruefe(gewaehlt.ToList()))
        {
            if (kandidat.Inhaltsschluessel is null)
            {
                ergebnis.Add(kandidat.Pfad);
                continue;
            }

            if (jeInhalt.TryAdd(kandidat.Inhaltsschluessel, kandidat.Pfad))
                ergebnis.Add(kandidat.Pfad);
        }

        return ergebnis;
    }

    private List<KanalQuellenkandidat> BaueKandidatenliste(
        string root,
        string? db3Path,
        string? viewerMdbPath,
        string? sdfPath,
        string? kinsTxtPath,
        string? gewaehlteXtf)
    {
        var liste = new List<KanalQuellenkandidat>();

        if (db3Path is not null)
            liste.Add(new KanalQuellenkandidat(db3Path, KanalQuellenart.Datenbank, "WinCan-Datenbank (db3)", true,
                "massgebliche Datenbank unter DB/"));

        if (viewerMdbPath is not null)
            liste.Add(new KanalQuellenkandidat(viewerMdbPath, KanalQuellenart.Datenbank, "WinCan-Viewer-Datenbank (mdb)",
                db3Path is null, db3Path is null ? "einzige Datenbank unter DB/" : "db3 vorhanden und bevorzugt"));

        if (sdfPath is not null)
        {
            liste.Add(new KanalQuellenkandidat(sdfPath, KanalQuellenart.Datenbank, "WinCan-VX-Datenbank (sdf)", false,
                "SQL Server Compact — unter .NET nicht direkt lesbar; es gilt der XTF-Ersatzweg"));
        }

        if (kinsTxtPath is not null)
            liste.Add(new KanalQuellenkandidat(kinsTxtPath, KanalQuellenart.Textdatei, "KINS-DVD (kiDVDaten.txt)", true,
                "eindeutiger DVD-Marker"));

        var kandidaten = PruefeXtfDateien(root)
            .Select(x => new XtfExportKandidat(x.Pfad, x.Merkmale))
            .ToList();
        var wahl = XtfExportAuswahl.Waehle(kandidaten);

        foreach (var entscheid in wahl.Entscheide)
        {
            var merkmale = kandidaten.First(k => k.Pfad == entscheid.Pfad).Merkmale;
            var art = XtfQuellenklassifikation.Art(merkmale);
            var familie = XtfQuellenklassifikation.Familie(merkmale);
            var uebernommen = entscheid.Uebernommen
                              && string.Equals(entscheid.Pfad, gewaehlteXtf, StringComparison.OrdinalIgnoreCase);

            var grund = entscheid.Uebernommen && !uebernommen
                ? entscheid.Grund + " — von diesem Importweg derzeit nicht verwendet"
                : entscheid.Grund;

            liste.Add(new KanalQuellenkandidat(
                entscheid.Pfad, KanalQuellenart.Xtf, $"XTF, {familie}, {art}", uebernommen, grund));
        }

        return liste;
    }

    /// <summary>
    /// Sucht die WinCan-VX-Datenbank (<c>.sdf</c>) unter einem Ordner "DB".
    /// Dieselbe Einschraenkung wie bei db3/mdb — sonst lenkt irgendeine fremde
    /// SQL-Compact-Datei im Kundenordner den Import um. <c>*_Meta.sdf</c> ist die
    /// Metadatenbank und nie die Fachquelle.
    /// </summary>
    private static string? FindWinCanSdf(string root)
    {
        string? best = null;
        long bestSize = -1;

        foreach (var path in SafeFileEnumeration.EnumerateFilesSafe(root, "*", recursive: true))
        {
            if (!Path.GetExtension(path).Equals(".sdf", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsUnderDbFolder(path))
                continue;
            if (Path.GetFileNameWithoutExtension(path).EndsWith("_Meta", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var size = new FileInfo(path).Length;
                if (size > bestSize)
                {
                    bestSize = size;
                    best = path;
                }
            }
            catch { /* nicht zugreifbar -> ueberspringen */ }
        }

        return best;
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

    // Der frueher hier stehende ReadXtfHeader las die ersten 64 KiB als Text und suchte
    // darin eine Zeichenfolge. Beides ist ersetzt durch XtfQuellenPruefer: Ein langer
    // Kopf schob den Modellnamen aus dem Fenster, und der Kopf sagt ohnehin nichts
    // darueber, ob die Datei Untersuchungen enthaelt (Audit 2026-09-05).
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
