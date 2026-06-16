using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Map;

// Testet die Dichtheits-Verteilung der echten KIT-Pruefberichte – mit und ohne Kataster-Abgleich.
// Aufruf: dotnet run --project tools/DichtheitDistributeTest -- [pdf1 pdf2 ...]

var xtf = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf";
var pdfs = args.Where(a => a.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
if (pdfs.Count == 0)
{
    pdfs = new List<string>
    {
        @"H:\swisstransfer_7be46e63-60ff-4ddb-babc-57b47edf822d\KIT-Prüfberichte - 22.05.2026.pdf",
        @"H:\swisstransfer_7be46e63-60ff-4ddb-babc-57b47edf822d\KIT-Prüfberichte - 11.05.2026.pdf",
        @"H:\swisstransfer_7be46e63-60ff-4ddb-babc-57b47edf822d\KIT-Prüfberichte - 13.05.2026.pdf",
    };
}

foreach (var p in pdfs)
    Console.WriteLine($"PDF: {p}  {(File.Exists(p) ? "" : "(NICHT GEFUNDEN)")}");
Console.WriteLine();

if (args.Contains("--dump"))
{
    var extraction = AuswertungPro.Next.Infrastructure.Import.Pdf.PdfTextExtractor.ExtractPages(pdfs[0]);
    Console.WriteLine($"ExtractPages: {extraction.Pages.Count} Seiten");
    for (var i = 0; i < extraction.Pages.Count && i < 6; i++)
    {
        var t = (extraction.Pages[i] ?? "").Replace("\r\n", "\n");
        Console.WriteLine($"---------- Seite {i + 1} ({t.Length} Zeichen) ----------");
        Console.WriteLine(t.Length > 1600 ? t[..1600] : t);
        Console.WriteLine();
    }
    return 0;
}

var idx = HaltungCadastreIndex.EnsureAndLoad(xtf);
Console.WriteLine($"Kataster-Index: {idx.Count} Schacht-Paare");
Console.WriteLine();

RunAndPrint("OHNE Kataster (alte Logik)", "ss_dichtheit_ohne", null);
RunAndPrint("MIT Kataster (neu)", "ss_dichtheit_mit", idx);

return 0;

void RunAndPrint(string title, string sub, IHaltungCadastreResolver? cad)
{
    var dest = Path.Combine(Path.GetTempPath(), sub);
    if (Directory.Exists(dest)) { try { Directory.Delete(dest, true); } catch { } }
    Directory.CreateDirectory(dest);

    var results = HoldingFolderDistributor.DistributeDichtheitFiles(
        pdfs, dest, moveInsteadOfCopy: false, overwrite: true, project: null, progress: null, cadastre: cad);

    Console.WriteLine($"=== {title} ===");
    int ok = 0, fail = 0;
    foreach (var r in results)
    {
        var folder = string.IsNullOrEmpty(r.HoldingFolder)
            ? ""
            : Path.GetFileName(r.HoldingFolder!.TrimEnd('\\', '/'));
        Console.WriteLine($"  [{(r.Success ? "OK " : "FEHL")}] {folder,-16} {r.Message}");
        if (r.Success) ok++; else fail++;
    }
    Console.WriteLine($"  => {ok} ok, {fail} fehlgeschlagen, {results.Count} gesamt");
    Console.WriteLine();
}
