using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Domain.Models;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests;

// Diagnose-Test (kein Regression-Test): Erstellt einen Bericht ueber die Verteilung
// fuer das echte Projekt D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426.
// Wird per "dotnet test --filter Category=Diag" ausgefuehrt.
[Trait("Category", "Integration")]
public sealed class ErstfeldJagdmattDiagnoseTests
{
    private readonly ITestOutputHelper _out;

    public ErstfeldJagdmattDiagnoseTests(ITestOutputHelper output) => _out = output;

    private const string ExportRoot = @"D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export";
    private static readonly string PdfFolder = Path.Combine(ExportRoot, "Report");
    private static readonly string VideoFolder = Path.Combine(ExportRoot, "Film");

    [Fact(DisplayName = "Diag: PDF-Distribution Erstfeld_Jagdmatt")]
    [Trait("Category", "Diag")]
    public void Diag_DistributePdf()
    {
        if (!Directory.Exists(ExportRoot))
        {
            _out.WriteLine($"SKIP: {ExportRoot} nicht vorhanden");
            return;
        }

        var dest = Path.Combine(Path.GetTempPath(), "sewer_diag_dist_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);

        try
        {
            var results = HoldingFolderDistributor.Distribute(
                pdfSourceFolder: PdfFolder,
                videoSourceFolder: VideoFolder,
                destGemeindeFolder: dest,
                moveInsteadOfCopy: false,
                overwrite: false,
                recursiveVideoSearch: true,
                unmatchedFolderName: "__UNMATCHED",
                project: null,
                progress: null);

            var ok = results.Count(r => r.Success);
            var matched = results.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Matched);
            var matchedNoDate = results.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.MatchedWithoutDate);
            var missing = results.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.NotFound);
            var ambig = results.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Ambiguous);

            _out.WriteLine($"Total results: {results.Count} (OK={ok})");
            _out.WriteLine($"Video: Matched={matched}, MatchedWithoutDate={matchedNoDate}, Missing={missing}, Ambiguous={ambig}");
            _out.WriteLine("");
            _out.WriteLine("--- ALLE Ergebnisse ---");
            foreach (var r in results.OrderBy(x => x.SourcePdfPath))
            {
                _out.WriteLine($"{(r.Success ? "OK  " : "FAIL")} | Video={r.VideoStatus,-20} | {Path.GetFileName(r.SourcePdfPath)} | Msg={r.Message}");
            }
        }
        finally
        {
            try { Directory.Delete(dest, recursive: true); } catch { }
        }
    }

    [Fact(DisplayName = "Diag: IBAK-Inspektionsprofile aus Erstfeld_Jagdmatt")]
    [Trait("Category", "Diag")]
    public void Diag_IbakInspectionProfiles()
    {
        if (!Directory.Exists(ExportRoot))
        {
            _out.WriteLine($"SKIP: {ExportRoot} fehlt");
            return;
        }

        var profiles = AuswertungPro.Next.Infrastructure.Import.Ibak.IbakInspectionProfileExtractor.ExtractFromExportRoot(ExportRoot);
        _out.WriteLine($"Profile: {profiles.Count}");

        var withVideo = profiles.Count(p => !string.IsNullOrEmpty(p.VideoPfad));
        var withLength = profiles.Count(p => p.LaengeM.HasValue);
        var withBcd = profiles.Count(p => !p.QualityFlags.MissingBcd);
        var withBce = profiles.Count(p => !p.QualityFlags.MissingBce);
        var totalEvents = profiles.Sum(p => p.Ereignisse.Count);

        _out.WriteLine($"mit Video:   {withVideo}/{profiles.Count}");
        _out.WriteLine($"mit Laenge:  {withLength}/{profiles.Count}");
        _out.WriteLine($"mit BCD:     {withBcd}/{profiles.Count}");
        _out.WriteLine($"mit BCE:     {withBce}/{profiles.Count}");
        _out.WriteLine($"Beobachtungen total: {totalEvents}");

        _out.WriteLine("\n--- Top 5 Profile ---");
        foreach (var p in profiles.Take(5))
        {
            _out.WriteLine($"  {p.HaltungKey,-25} | L={p.LaengeM:F2}m | dt={p.DauerSekunden:F0}s | events={p.Ereignisse.Count} | video={Path.GetFileName(p.VideoPfad)}");
        }
    }

    [Fact(DisplayName = "Diag: KIAS-Pattern-Erkennung Erstfeld_Jagdmatt")]
    [Trait("Category", "Diag")]
    public void Diag_KiasPatternDetect()
    {
        if (!Directory.Exists(ExportRoot))
        {
            _out.WriteLine($"SKIP: {ExportRoot} fehlt");
            return;
        }

        var d = AuswertungPro.Next.Infrastructure.Import.Ibak.KiasExportPattern.Detect(ExportRoot);
        _out.WriteLine($"IsKias:                 {d.IsKias}");
        _out.WriteLine($"HasArizonaFdb:          {d.HasArizonaFdb}");
        _out.WriteLine($"HasFilmFolder:          {d.HasFilmFolder}");
        _out.WriteLine($"HasReportFolder:        {d.HasReportFolder}");
        _out.WriteLine($"HasDatenTxt:            {d.HasDatenTxt}");
        _out.WriteLine($"HoldingPdfCount (H_*):  {d.HoldingPdfCount}");
        _out.WriteLine($"LateralPdfCount (L_*):  {d.LateralPdfCount}");
        _out.WriteLine($"~G-Gegenrichtung:       {d.GegenrichtungVideoCount}");
        _out.WriteLine($"~N-Wiederholungen:      {d.RepeatTakeVideoCount}");
        _out.WriteLine($"Reason: {d.Reason}");

        Assert.True(d.IsKias, "KIAS-Export muss erkannt werden");
        Assert.True(d.HoldingPdfCount > 0, "Mindestens ein H_*.pdf erwartet");
        Assert.True(d.HasArizonaFdb, "Arizona.fdb muss gefunden werden");
    }

    [Fact(DisplayName = "Diag: NotFound L-PDFs - Parser-Output je Split")]
    [Trait("Category", "Diag")]
    public void Diag_ParserHaltungOnLPdfs()
    {
        if (!Directory.Exists(ExportRoot))
        {
            _out.WriteLine($"SKIP: {ExportRoot} fehlt");
            return;
        }

        var problemPdfs = new[]
        {
            "L_438.01-36052.pdf",
            "L_438.03-438.04.pdf",
            "L_1273.01-7.34854.pdf",
            "L_33424-10.142800.pdf",
            "L_33425-33427.pdf",
            "L_33438-1273.09.pdf",
        };

        // Sample-PDF: pdftotext -layout, dann ParsePdfPage je Seite anschauen
        foreach (var name in problemPdfs)
        {
            var path = Path.Combine(PdfFolder, name);
            if (!File.Exists(path))
            {
                _out.WriteLine($"SKIP: {name} nicht da");
                continue;
            }

            try
            {
                var ext = AuswertungPro.Next.Infrastructure.Import.Pdf.PdfTextExtractor.ExtractPages(path);
                _out.WriteLine($"\n=== {name} ({ext.Pages.Count} Seiten) ===");

                for (var p = 0; p < Math.Min(ext.Pages.Count, 8); p++)
                {
                    var parsed = HoldingFolderDistributor.ParsePdfPage(ext.Pages[p], path);
                    _out.WriteLine($"  Seite {p + 1}: Success={parsed.Success}, Haltung={parsed.Haltung}, Date={parsed.Date}, Video={parsed.VideoFile}, Msg={parsed.Message}");
                }
            }
            catch (Exception ex)
            {
                _out.WriteLine($"  FEHLER: {ex.Message}");
            }
        }
    }

    [Fact(DisplayName = "Diag: IBAK-Import Erstfeld_Jagdmatt")]
    [Trait("Category", "Diag")]
    public void Diag_ImportIbak()
    {
        if (!Directory.Exists(ExportRoot))
        {
            _out.WriteLine($"SKIP: {ExportRoot} nicht vorhanden");
            return;
        }

        var project = new Project { Name = "Diag" };
        var importer = new IbakExportImportService();
        var result = importer.ImportIbakExport(ExportRoot, project);

        if (!result.Ok)
        {
            _out.WriteLine($"IBAK Import FAIL: {result.ErrorCode} - {result.ErrorMessage}");
            return;
        }

        var stats = result.Value!;
        _out.WriteLine($"Found={stats.Found}, Created={stats.Created}, Updated={stats.Updated}, Errors={stats.Errors}, Uncertain={stats.Uncertain}");
        _out.WriteLine($"Records: {project.Data.Count}");
        _out.WriteLine("");
        _out.WriteLine("--- Stammdaten je Haltung ---");
        foreach (var rec in project.Data.OrderBy(r => r.GetFieldValue("Haltungsname") ?? ""))
        {
            var name = rec.GetFieldValue("Haltungsname") ?? "?";
            var dn = rec.GetFieldValue("DN_mm") ?? "-";
            var mat = rec.GetFieldValue("Rohrmaterial") ?? "-";
            var len = rec.GetFieldValue("Haltungslaenge_m") ?? "-";
            var link = rec.GetFieldValue("Link") ?? "-";
            var pdf = rec.GetFieldValue("PDF_Path") ?? "-";
            _out.WriteLine($"{name,-25} | DN={dn,5} | Mat={mat,-15} | L={len,5} | Video={Path.GetFileName(link),-30} | PDF={Path.GetFileName(pdf)}");
        }

        _out.WriteLine("");
        _out.WriteLine("--- Letzte Messages (max 30) ---");
        foreach (var m in stats.Messages.TakeLast(30))
            _out.WriteLine(m);
    }
}
