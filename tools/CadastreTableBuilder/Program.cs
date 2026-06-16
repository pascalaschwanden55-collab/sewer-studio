using System.Diagnostics;
using AuswertungPro.Next.Infrastructure.Map;

// CadastreTableBuilder — baut aus einer SIA405-XTF (Abwasserkataster) die eigenstaendige
// Haltungs-Tabelle (fest im SewerStudio-Ordner) und testet den Schacht-Paar-Nachschlag.
//
// Aufruf:
//   dotnet run --project tools/CadastreTableBuilder -- <xtf> [--out <tabelle.tsv>] [--lookup A B]

var xtf = args.Length > 0 && !args[0].StartsWith("--")
    ? args[0]
    : @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf";

string? outPath = null;
var lookups = new List<(string A, string B)>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--out" && i + 1 < args.Length) outPath = args[++i];
    else if (args[i] == "--lookup" && i + 2 < args.Length) lookups.Add((args[++i], args[++i]));
}

var table = outPath ?? HaltungCadastreIndex.DefaultTablePath;

if (!File.Exists(xtf))
{
    Console.WriteLine($"FEHLER: XTF nicht gefunden: {xtf}");
    return 1;
}

Console.WriteLine($"XTF:     {xtf}");
Console.WriteLine($"Tabelle: {table}");
Console.WriteLine();

var sw = Stopwatch.StartNew();
var count = HaltungCadastreExtractor.BuildTable(xtf, table);
sw.Stop();

var fi = new FileInfo(table);
Console.WriteLine($"Haltungen extrahiert: {count} in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"Tabellen-Groesse:     {fi.Length / 1024.0:F0} KB");

var idx = HaltungCadastreIndex.Load(table);
Console.WriteLine($"Index (Schacht-Paare → Haltung): {idx.Count}");
Console.WriteLine();

void Test(string a, string b)
{
    var ok = idx.TryResolvePair(a, b, out var c);
    Console.WriteLine($"  Paar {a,-8}/ {b,-8} → {(ok ? c : "(kein eindeutiger Treffer)")}");
}

Console.WriteLine("Test-Nachschlag (auch vertauscht):");
if (lookups.Count > 0)
{
    foreach (var (a, b) in lookups) Test(a, b);
}
else
{
    Test("865", "864");        // dein Fall
    Test("864", "865");        // vertauscht → muss dieselbe Haltung liefern
    Test("31955", "30882");    // erste Haltung im Kataster
    Test("999999", "888888");  // existiert nicht
}

var cand = idx.ResolveFromCandidates(new[] { "200", "865", "864", "250" });
Console.WriteLine();
Console.WriteLine($"Universell aus Kandidaten [200, 865, 864, 250]: " +
                  (cand.Count > 0 ? string.Join(", ", cand) : "(nichts)"));
return 0;
