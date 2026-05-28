// Stammdaten-Exporter (Schritt 2b von Plan v2)
//
// Ruft den bestehenden StammdatenAggregator (XTF + PDF + FDB) pro Cadaster-
// Projekt-Export auf und schreibt eine konsolidierte haltungs_stammdaten.json,
// die kb_context_dryrun.py / kb_context_apply.py konsumieren.
//
// Aufruf:
//   StammdatenExporter --root <PfadZurExportSammlung> [--root <weitererPfad> ...]
//                      [--out C:\KI_BRAIN\stammdaten\haltungs_stammdaten.json]
//                      [--single]
//
// Verhalten:
//   * --root kann mehrfach genannt werden. Pro --root wird zuerst geprueft, ob
//     der Pfad selbst ein Export-Root ist (XTF/PDF/FDB direkt darin); wenn nein,
//     werden alle direkten Unterordner als Export-Roots behandelt.
//   * --single zwingt das Tool, --root ausschliesslich als Einzel-Export zu
//     behandeln (kein Auto-Scan der Unterordner).
//   * Bei Konflikt zwischen Projekten gewinnt die hoehere Quellprioritaet (XTF >
//     PDF > FDB). Bei gleicher Prioritaet gewinnt der erste gesehene Eintrag.
//   * Es wird AUSSCHLIESSLICH eine JSON-Datei geschrieben — keine DB beruehrt.
//
// Output-Format (Map<HaltungsKey, Eintrag>) — gespiegelt im Python-Konsumenten:
//   {
//     "1.01-59007":      { "Material": "Beton",     "DN_mm": 300, "Source": "Pdf", ... },
//     "06.691078-691070": { "Material": "Steinzeug", "DN_mm": 250, "Source": "Xtf", ... }
//   }
//
// Es wird KEINE Schreibaktion in den Quellordnern ausgefuehrt — das Tool ist
// rein lesend, abgesehen vom JSON-Output.

using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Import.Ibak;

var roots = new List<string>();
var haltungsFolders = new List<string>();
string outPath = @"C:\KI_BRAIN\stammdaten\haltungs_stammdaten.json";
bool singleMode = false;

for (var i = 0; i < args.Length; i++)
{
    var a = args[i];
    switch (a)
    {
        case "--root" or "-r":
            if (i + 1 >= args.Length) { PrintUsageAndExit(2); return; }
            roots.Add(args[++i]);
            break;
        case "--out" or "-o":
            if (i + 1 >= args.Length) { PrintUsageAndExit(2); return; }
            outPath = args[++i];
            break;
        case "--single":
            singleMode = true;
            break;
        case "--haltungs-folders":
            // Modus fuer Verzeichnis, in dem JEDER Unterordner eine Haltung
            // ist (Ordnername = Haltungs-ID, drin liegen 1..n PDFs mit
            // beliebigem Namen wie '20230830_06.24341-35625.pdf'). Wird
            // separat vom Cadaster-Aggregator behandelt.
            if (i + 1 >= args.Length) { PrintUsageAndExit(2); return; }
            haltungsFolders.Add(args[++i]);
            break;
        case "--help" or "-h":
            PrintUsageAndExit(0); return;
        default:
            Console.Error.WriteLine($"Unbekannter Parameter: {a}");
            PrintUsageAndExit(2); return;
    }
}

if (roots.Count == 0 && haltungsFolders.Count == 0)
{
    Console.Error.WriteLine(
        "FEHLER: mindestens ein --root <Pfad> oder --haltungs-folders <Pfad> erforderlich.");
    PrintUsageAndExit(2);
    return;
}

// ── Export-Roots sammeln ──────────────────────────────────────────────────
var exportRoots = new List<string>();
foreach (var r in roots)
{
    if (!Directory.Exists(r))
    {
        Console.Error.WriteLine($"WARN: Pfad existiert nicht, uebersprungen: {r}");
        continue;
    }

    if (singleMode || LooksLikeExportRoot(r))
    {
        exportRoots.Add(r);
        continue;
    }

    var subdirs = Directory.EnumerateDirectories(r).Where(LooksLikeExportRoot).ToList();
    if (subdirs.Count == 0)
    {
        Console.Error.WriteLine(
            $"WARN: '{r}' enthaelt weder XTF/PDF/FDB direkt noch in 1. Ebene — uebersprungen.");
        continue;
    }
    Console.WriteLine($"[Auto-Scan] {r} -> {subdirs.Count} Export-Roots gefunden.");
    exportRoots.AddRange(subdirs);
}

if (exportRoots.Count == 0 && haltungsFolders.Count == 0)
{
    Console.Error.WriteLine("FEHLER: kein verwertbarer Export-Root gefunden. Abbruch.");
    Environment.Exit(3);
    return;
}

// ── Pro Export aggregieren und mergen ────────────────────────────────────--
var merged = new Dictionary<string, OutEntry>(StringComparer.OrdinalIgnoreCase);
var perRootStats = new List<(string Root, int Holdings, int FromXtf, int FromPdf, int FromFdb)>();

foreach (var er in exportRoots)
{
    Console.WriteLine($"  -> {er}");
    var msgs = new List<string>();
    var (map, stats) = StammdatenAggregator.Build(er, msgs);
    foreach (var m in msgs) Console.WriteLine($"     {m}");

    foreach (var (key, entry) in map)
    {
        var normKey = NormalizeKey(key);
        var newOut = new OutEntry(
            Material: entry.Material.Value,
            MaterialSource: entry.Material.Source.ToString(),
            DN_mm: entry.DN_mm.Value,
            DnSource: entry.DN_mm.Source.ToString(),
            Profilbreite_mm: entry.Profilbreite_mm.Value,
            Geometrie: entry.Geometrie.Value,
            GeometrieSource: entry.Geometrie.Source.ToString(),
            Nutzungsart: entry.Nutzungsart.Value,
            Laenge_m: entry.Laenge_m.Value,
            Strasse: entry.Strasse.Value,
            Ort: entry.Ort.Value,
            ProvenanceRoot: er);

        if (!merged.TryGetValue(normKey, out var cur))
        {
            merged[normKey] = newOut;
            continue;
        }
        merged[normKey] = MergeOut(cur, newOut);
    }
    perRootStats.Add((er, stats.TotalHoldings, stats.FromXtf, stats.FromPdf, stats.FromFdb));
}

// ── Haltungs-Folder-Modus ────────────────────────────────────────────────--
// Pro Unterordner = eine Haltung. Ordnername ist der Haltungs-Key. Wir nehmen
// die NEUESTE PDF im Ordner (nach Datums-Praefix bzw. Last-Modified-Time) und
// lassen den IbakPdfStammdatenExtractor sie parsen. Das deckt den Fall ab,
// wo PDFs nicht der Cadaster-Konvention 'H_*.pdf' folgen.
foreach (var hf in haltungsFolders)
{
    if (!Directory.Exists(hf))
    {
        Console.Error.WriteLine($"WARN: --haltungs-folders Pfad existiert nicht: {hf}");
        continue;
    }
    Console.WriteLine($"[Haltungs-Folder-Mode] {hf}");
    int processed = 0, hfWithMaterial = 0, hfWithDn = 0, noPdf = 0, parseFailed = 0;

    // Rekursiv: jeder Unterordner (auf beliebiger Tiefe), dessen Name dem
    // Haltungs-Pattern '^[0-9.]+(-[0-9.]+)+$' folgt, wird als Haltung
    // betrachtet. Damit funktioniert sowohl 'D:/Haltungen/<ID>' als auch
    // 'D:/Videoprojekte/Zone X/<ID>'.
    var haltungsPattern = new System.Text.RegularExpressions.Regex(
        @"^[0-9.]+(-[0-9.]+)+$");
    IEnumerable<string> CollectHaltungsDirs(string startDir)
    {
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(startDir); }
        catch (UnauthorizedAccessException) { yield break; }
        catch (PathTooLongException) { yield break; }
        foreach (var d in dirs)
        {
            var name = Path.GetFileName(d) ?? "";
            if (haltungsPattern.IsMatch(name))
                yield return d;
            else
                foreach (var inner in CollectHaltungsDirs(d))
                    yield return inner;
        }
    }

    foreach (var dir in CollectHaltungsDirs(hf))
    {
        var folderName = Path.GetFileName(dir);
        if (string.IsNullOrWhiteSpace(folderName)) continue;
        var key = NormalizeKey(folderName);
        if (string.IsNullOrWhiteSpace(key)) continue;

        // Neueste PDF gewinnt (Datums-Praefix sortiert lexikografisch absteigend
        // korrekt fuer YYYYMMDD-Format; Fallback Last-Modified).
        var pdfs = Directory.EnumerateFiles(dir, "*.pdf").ToList();
        if (pdfs.Count == 0) { noPdf++; continue; }
        pdfs.Sort((a, b) =>
        {
            var na = Path.GetFileName(a) ?? "";
            var nb = Path.GetFileName(b) ?? "";
            var c = string.Compare(nb, na, StringComparison.Ordinal); // descending
            if (c != 0) return c;
            return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
        });

        IbakPdfStammdatenExtractor.StammdatenResult? sd = null;
        foreach (var pdf in pdfs)
        {
            sd = IbakPdfStammdatenExtractor.Extract(pdf);
            if (sd is not null && (sd.Material is not null || sd.DN_mm is not null))
                break;
        }
        if (sd is null) { parseFailed++; continue; }

        processed++;
        if (!string.IsNullOrWhiteSpace(sd.Material)) hfWithMaterial++;
        if (sd.DN_mm is > 0) hfWithDn++;

        var newOut = new OutEntry(
            Material: sd.Material,
            MaterialSource: sd.Material is not null ? "Pdf" : "None",
            DN_mm: sd.DN_mm,
            DnSource: sd.DN_mm is > 0 ? "Pdf" : "None",
            Profilbreite_mm: sd.Profilbreite_mm,
            Geometrie: sd.Geometrie,
            GeometrieSource: sd.Geometrie is not null ? "Pdf" : "None",
            Nutzungsart: sd.Nutzungsart,
            Laenge_m: sd.Laenge_m,
            Strasse: null,
            Ort: null,
            ProvenanceRoot: dir);

        if (!merged.TryGetValue(key, out var cur))
            merged[key] = newOut;
        else
            merged[key] = MergeOut(cur, newOut);
    }
    Console.WriteLine(
        $"  Haltungen verarbeitet: {processed}, Material: {hfWithMaterial}, DN: {hfWithDn}, " +
        $"ohne PDF: {noPdf}, Parse-Fehler: {parseFailed}");
}

// ── Output schreiben ─────────────────────────────────────────────────────--
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = null,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
File.WriteAllText(outPath, JsonSerializer.Serialize(merged, jsonOpts));

// ── Zusammenfassung ──────────────────────────────────────────────────────--
Console.WriteLine();
Console.WriteLine($"=== Stammdaten-Export fertig ===");
Console.WriteLine($"Aggregierte Haltungen (eindeutig): {merged.Count:N0}");
Console.WriteLine($"Output: {outPath}");
Console.WriteLine();
Console.WriteLine("Pro Export-Root:");
foreach (var s in perRootStats)
    Console.WriteLine($"  {s.Holdings,5} Hltg | XTF:{s.FromXtf,4} PDF:{s.FromPdf,4} FDB:{s.FromFdb,4} | {s.Root}");

var withMat = merged.Values.Count(e => !string.IsNullOrWhiteSpace(e.Material));
var withDn  = merged.Values.Count(e => e.DN_mm is > 0);
Console.WriteLine();
Console.WriteLine($"Coverage im Output:");
Console.WriteLine($"  Mit Material: {withMat:N0} ({Pct(withMat, merged.Count)}%)");
Console.WriteLine($"  Mit DN_mm:    {withDn:N0} ({Pct(withDn, merged.Count)}%)");

// ── Process-Exit hart erzwingen ──────────────────────────────────────────--
// Hintergrund: Die Firebird-Native-Library (fbembed.dll) registriert einen
// AppDomain.ProcessExit-Hook (ShutdownHelper.HandleProcessShutdown), der bei
// JEDEM Process-Exit fb_shutdown() im native Client aufruft. Wenn die Bitness
// der DLL nicht zur Runtime passt (BadImageFormatException 0x8007000B, hier:
// 32-bit fbembed im 64-bit .NET-Prozess), wirft dieser Hook waehrend des Exits
// eine UnhandledException und der Prozess beendet mit 0xE0434352 — auch wenn
// die JSON-Ausgabe bereits sauber geschrieben wurde. Environment.Exit(0)
// reicht NICHT, weil die ProcessExit-Hooks danach trotzdem laufen und den
// Exit-Code ueberschreiben. Auch ein eigener UnhandledException-Handler
// kann den nativen Crash nicht stoppen.
//
// Saubere Loesung: kernel32!ExitProcess direkt aufrufen. Das umgeht die
// Managed-Shutdown-Pipeline komplett, keine ProcessExit-Hooks laufen mehr,
// der Exit-Code steht und ist genau so wie wir ihn setzen.
Console.Out.Flush();
Console.Error.Flush();
ExitProcess(0);
return;

[DllImport("kernel32.dll", SetLastError = false)]
static extern void ExitProcess(uint exitCode);

// ── Helpers ──────────────────────────────────────────────────────────────--

static void PrintUsageAndExit(int code)
{
    Console.Error.WriteLine("""
        StammdatenExporter [--root <Pfad>...] [--haltungs-folders <Pfad>...]
                           [--out <json>] [--single]

        Modi:
          --root <Pfad>             Stammdaten-DB-Export-Root (XTF + PDF + FDB).
                                     Mehrfach moeglich. Ohne --single werden
                                     direkte Unterordner als Roots behandelt.
          --haltungs-folders <Pfad> Verzeichnis, in dem JEDER Unterordner
                                     einer Haltung entspricht (Ordnername =
                                     Haltungs-ID). Es wird die neueste PDF
                                     im Ordner geparst.

        KEINE DB-Aenderungen, KEINE Schreibzugriffe auf die Quellordner.
        """);
    Environment.Exit(code);
}

static bool LooksLikeExportRoot(string dir)
{
    if (!Directory.Exists(dir)) return false;
    try
    {
        // Schnellpruefung: irgendwo unter dem Root muss es .xtf, .pdf oder .fdb geben.
        if (Directory.EnumerateFiles(dir, "*.xtf",  SearchOption.AllDirectories).Any()) return true;
        if (Directory.EnumerateFiles(dir, "H_*.pdf", SearchOption.AllDirectories).Any()) return true;
        if (Directory.EnumerateFiles(dir, "L_*.pdf", SearchOption.AllDirectories).Any()) return true;
        if (Directory.EnumerateFiles(dir, "*.fdb",  SearchOption.AllDirectories).Any()) return true;
    }
    catch (UnauthorizedAccessException) { return false; }
    catch (PathTooLongException)        { return false; }
    return false;
}

static string NormalizeKey(string raw)
    => (raw ?? string.Empty).Replace(" ", "").Replace("/", "-").Replace("–", "-").Replace("—", "-");

static string Pct(int n, int total) =>
    total == 0 ? "0" : (100.0 * n / total).ToString("F1", CultureInfo.InvariantCulture);

// Bei Mehrfach-Treffern denselben Source-Prio-Vergleich anwenden, den der
// Aggregator pro Export liefert. Der Source-Enum-Wert ist als String gespeichert,
// wir mappen ihn auf einen numerischen Rang (kleiner = hoeher prio).
static OutEntry MergeOut(OutEntry cur, OutEntry next)
{
    static int Rank(string? s) => s switch
    {
        "Xtf" => 1,
        "Pdf" => 2,
        "Fdb" => 3,
        _      => 99,
    };

    string? mat = cur.Material;
    string? matSrc = cur.MaterialSource;
    if (!string.IsNullOrWhiteSpace(next.Material)
        && (string.IsNullOrWhiteSpace(mat) || Rank(next.MaterialSource) < Rank(matSrc)))
    {
        mat = next.Material;
        matSrc = next.MaterialSource;
    }

    int? dn = cur.DN_mm;
    string? dnSrc = cur.DnSource;
    if (next.DN_mm is > 0 && (dn is null or 0 || Rank(next.DnSource) < Rank(dnSrc)))
    {
        dn = next.DN_mm;
        dnSrc = next.DnSource;
    }

    string? geo = cur.Geometrie;
    string? geoSrc = cur.GeometrieSource;
    if (!string.IsNullOrWhiteSpace(next.Geometrie)
        && (string.IsNullOrWhiteSpace(geo) || Rank(next.GeometrieSource) < Rank(geoSrc)))
    {
        geo = next.Geometrie;
        geoSrc = next.GeometrieSource;
    }

    return cur with
    {
        Material = mat,
        MaterialSource = matSrc,
        DN_mm = dn,
        DnSource = dnSrc,
        Geometrie = geo,
        GeometrieSource = geoSrc,
        Profilbreite_mm = cur.Profilbreite_mm ?? next.Profilbreite_mm,
        Nutzungsart = cur.Nutzungsart ?? next.Nutzungsart,
        Laenge_m = cur.Laenge_m ?? next.Laenge_m,
        Strasse = cur.Strasse ?? next.Strasse,
        Ort = cur.Ort ?? next.Ort,
    };
}

internal sealed record OutEntry(
    string? Material,
    string? MaterialSource,
    int? DN_mm,
    string? DnSource,
    int? Profilbreite_mm,
    string? Geometrie,
    string? GeometrieSource,
    string? Nutzungsart,
    double? Laenge_m,
    string? Strasse,
    string? Ort,
    string ProvenanceRoot);
