// PdfCoverageAudit - read-only Coverage-Check des echten Protokoll-Parsers.
// Laeuft mit dem ECHTEN PdfProtocolExtractor ueber alle PDFs unter den angegebenen
// Wurzeln und meldet, welche PDFs 0 Befunde liefern (= nicht erkannte Formate).
// Aendert NICHTS an den PDFs oder der KB. Schreibt nur einen CSV-Report.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// Wurzeln: aus Argumenten, sonst die zwei bekannten Orte.
string[] roots = args.Length > 0
    ? args
    : new[]
    {
        @"D:\Haltungen",
        @"H:\02_Sanierung_Abnahmedoku_Kunde_25100490",
    };

var reportPath = @"C:\tmp\pdf_coverage_report.csv";
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== PDF-Coverage-Audit (echter PdfProtocolExtractor, read-only) ===");

// Alle PDFs sammeln (rekursiv, dedupliziert).
var pdfs = new List<string>();
var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var root in roots)
{
    if (!Directory.Exists(root))
    {
        Console.WriteLine($"  WARNUNG: Wurzel nicht gefunden, uebersprungen: {root}");
        continue;
    }
    Console.WriteLine($"  Wurzel: {root}");
    foreach (var f in Directory.EnumerateFiles(root, "*.pdf", SearchOption.AllDirectories))
    {
        if (seen.Add(Path.GetFullPath(f)))
            pdfs.Add(f);
    }
}

Console.WriteLine($"  Gefundene PDFs gesamt: {pdfs.Count}");
Console.WriteLine();

var extractor = new PdfProtocolExtractor();

int ok = 0, empty = 0, error = 0;
var rows = new List<(string Path, int Count, string Status, string Sample, string Error)>();
var sw = Stopwatch.StartNew();

for (int i = 0; i < pdfs.Count; i++)
{
    var path = pdfs[i];
    string status, sample = "", err = "";
    int count = 0;
    try
    {
        var entries = await extractor.ExtractAsync(path);
        count = entries.Count;
        if (count > 0)
        {
            ok++;
            status = "OK";
            sample = string.Join("|", entries.Take(5).Select(e => e.VsaCode));
        }
        else
        {
            empty++;
            status = "EMPTY";
        }
    }
    catch (Exception ex)
    {
        error++;
        status = "ERROR";
        err = ex.GetType().Name + ": " + ex.Message.Replace("\r", " ").Replace("\n", " ");
    }

    rows.Add((path, count, status, sample, err));

    if ((i + 1) % 100 == 0 || i == pdfs.Count - 1)
        Console.WriteLine($"  ... {i + 1}/{pdfs.Count}  (OK={ok}  EMPTY={empty}  ERROR={error})");
}

sw.Stop();

// CSV schreiben.
var csv = new StringBuilder();
csv.AppendLine("Status;Befunde;Pfad;Beispiel-Codes;Fehler");
foreach (var r in rows.OrderBy(r => r.Status).ThenByDescending(r => r.Count))
    csv.AppendLine(string.Join(";",
        r.Status,
        r.Count.ToString(CultureInfo.InvariantCulture),
        Csv(r.Path),
        Csv(r.Sample),
        Csv(r.Error)));
File.WriteAllText(reportPath, csv.ToString(), new UTF8Encoding(true));

// Zusammenfassung.
int total = pdfs.Count;
double cov = total > 0 ? 100.0 * ok / total : 0;
Console.WriteLine();
Console.WriteLine("================ ERGEBNIS ================");
Console.WriteLine($"  PDFs gesamt:      {total}");
Console.WriteLine($"  Erkannt (>=1):    {ok}   ({cov:F1} %)");
Console.WriteLine($"  0 Befunde:        {empty}");
Console.WriteLine($"  Fehler/Crash:     {error}");
Console.WriteLine($"  Dauer:            {sw.Elapsed.TotalSeconds:F0} s");
Console.WriteLine($"  Report:           {reportPath}");

// Luecken nach Ordner gruppieren (immediate Unterordner der jeweiligen Wurzel),
// damit ein komplett fehlendes Format/Firma sofort auffaellt.
var gaps = rows.Where(r => r.Status != "OK").ToList();
if (gaps.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("  --- Luecken (0 Befunde / Fehler) nach Top-Ordner ---");
    var byFolder = gaps
        .GroupBy(r => TopFolder(r.Path, roots))
        .OrderByDescending(g => g.Count())
        .Take(25);
    foreach (var g in byFolder)
        Console.WriteLine($"    {g.Count(),5}x  {g.Key}");

    Console.WriteLine();
    Console.WriteLine("  --- Erste 15 betroffene PDFs ---");
    foreach (var r in gaps.Take(15))
        Console.WriteLine($"    [{r.Status}] {r.Path}{(string.IsNullOrEmpty(r.Error) ? "" : "  -> " + r.Error)}");
}

return 0;

static string Csv(string s)
{
    s ??= "";
    if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    return s;
}

// Liefert den ersten Pfadteil unter der passenden Wurzel (z.B. Haltungs-/Firmen-Ordner).
static string TopFolder(string path, string[] roots)
{
    foreach (var root in roots)
    {
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var rel = path.Substring(root.Length).TrimStart('\\', '/');
            var parts = rel.Split('\\', '/');
            return parts.Length > 1 ? Path.Combine(root, parts[0]) : root;
        }
    }
    return Path.GetDirectoryName(path) ?? path;
}
