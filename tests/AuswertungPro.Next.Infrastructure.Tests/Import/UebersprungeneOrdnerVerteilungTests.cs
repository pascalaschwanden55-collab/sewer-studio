using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// R1 (Deepscan 02.10.2026): Verteilwege melden Quellordner, die die sichere Dateisuche
/// auslaesst, als Fehler. Die Verknuepfung selbst bleibt unbetreten.
/// </summary>
public sealed class UebersprungeneOrdnerVerteilungTests
{
    [JunctionFact]
    public void Dichtheitsverteilung_fuehrt_den_uebersprungenen_Ordner_in_der_Fehlerliste()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-dp");
        File.WriteAllText(Path.Combine(baum.Extern, "20260814_100-200_DP.pdf"), "liegt hinter der Verknüpfung");
        var link = baum.Verknuepfe(baum.Quelle);
        var projekt = Path.Combine(baum.Wurzel, "projekt");
        Directory.CreateDirectory(projekt);

        var ergebnis = new DichtheitImportDistributionService().Distribute(new Project(), projekt, baum.Quelle);

        Assert.Contains(UebersprungeneOrdnerTestbaum.ErwarteteZeile(link), ergebnis.FehlerListe);
        Assert.Contains(UebersprungeneOrdnerTestbaum.ErwarteteZeile(link), ergebnis.Messages);
    }

    [JunctionFact]
    public void Haltungsverteilung_meldet_uebersprungene_PDF_Ordner_mit_und_ohne_gefundene_PDFs()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-haltung-pdf");
        File.WriteAllText(Path.Combine(baum.Extern, "protokoll.pdf"), "liegt hinter der Verknüpfung");
        var leer = Path.Combine(baum.Wurzel, "nur-verknuepfung");
        var leerLink = baum.Verknuepfe(leer);
        var mitPdf = Path.Combine(baum.Wurzel, "mit-pdf");
        Directory.CreateDirectory(mitPdf);
        File.WriteAllText(Path.Combine(mitPdf, "kaputt.pdf"), "kein echtes PDF");
        var mitPdfLink = baum.Verknuepfe(mitPdf);
        var video = Path.Combine(baum.Wurzel, "video");
        Directory.CreateDirectory(video);

        var ohnePdf = HoldingFolderDistributor.Distribute(leer, video, Path.Combine(baum.Wurzel, "ziel-a"));
        var mitGefundenemPdf = HoldingFolderDistributor.Distribute(mitPdf, video, Path.Combine(baum.Wurzel, "ziel-b"));

        AssertFehlerergebnis(ohnePdf, leerLink);
        Assert.Contains(ohnePdf, r => r.Message.StartsWith("No PDF files found", StringComparison.Ordinal));
        AssertFehlerergebnis(mitGefundenemPdf, mitPdfLink);
        Assert.Contains(mitGefundenemPdf, r => r.SourcePdfPath.EndsWith("kaputt.pdf", StringComparison.OrdinalIgnoreCase));
    }

    [JunctionFact]
    public void Haltungsverteilung_meldet_uebersprungene_Videoordner()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-haltung-video");
        File.WriteAllText(Path.Combine(baum.Extern, "100-200.mp4"), "liegt hinter der Verknüpfung");
        var pdfOrdner = Path.Combine(baum.Wurzel, "pdf");
        Directory.CreateDirectory(pdfOrdner);
        var pdf = Path.Combine(pdfOrdner, "kaputt.pdf");
        File.WriteAllText(pdf, "kein echtes PDF");
        var link = baum.Verknuepfe(baum.Quelle);

        var ausgewaehlt = HoldingFolderDistributor.DistributeFiles(
            new[] { pdf }, baum.Quelle, Path.Combine(baum.Wurzel, "ziel-a"));
        var ordner = HoldingFolderDistributor.Distribute(
            pdfOrdner, baum.Quelle, Path.Combine(baum.Wurzel, "ziel-b"));

        AssertFehlerergebnis(ausgewaehlt, link);
        AssertFehlerergebnis(ordner, link);
    }

    [JunctionFact]
    public void Txt_Schacht_und_Dichtheitsverteilung_melden_uebersprungene_Quellordner_ohne_Fund()
        => PruefeTxtSchachtUndDichtheit(mitGefundenerDatei: false);

    [JunctionFact]
    public void Txt_Schacht_und_Dichtheitsverteilung_melden_uebersprungene_Quellordner_mit_Fund()
        => PruefeTxtSchachtUndDichtheit(mitGefundenerDatei: true);

    private static void PruefeTxtSchachtUndDichtheit(bool mitGefundenerDatei)
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-weitere");
        File.WriteAllText(Path.Combine(baum.Extern, "kiDVDaten.txt"), "liegt hinter der Verknüpfung");
        if (mitGefundenerDatei)
        {
            // Mit gefundener Datei laeuft der eigentliche Verteilweg, sonst die Rueckmeldung «nichts gefunden».
            File.WriteAllText(Path.Combine(baum.Quelle, "kiDVDaten.txt"), "keine Haltung");
            File.WriteAllText(Path.Combine(baum.Quelle, "kaputt.pdf"), "kein echtes PDF");
        }

        var link = baum.Verknuepfe(baum.Quelle);
        var video = Path.Combine(baum.Wurzel, "video");
        Directory.CreateDirectory(video);

        var txt = HoldingFolderDistributor.DistributeTxt(baum.Quelle, video, Path.Combine(baum.Wurzel, "ziel-a"));
        var schacht = HoldingFolderDistributor.DistributeShafts(baum.Quelle, Path.Combine(baum.Wurzel, "ziel-b"));
        var dichtheit = HoldingFolderDistributor.DistributeDichtheit(baum.Quelle, Path.Combine(baum.Wurzel, "ziel-c"));

        AssertFehlerergebnis(txt, link);
        AssertFehlerergebnis(schacht, link);
        AssertFehlerergebnis(dichtheit, link);
        Assert.Equal(mitGefundenerDatei, !dichtheit.Any(r => r.Message.StartsWith("No PDF files found", StringComparison.Ordinal)));
    }

    [JunctionFact]
    public void Schachtverteilung_nennt_den_Grund_mit_dem_gemeinsamen_Text()
    {
        using var baum = new UebersprungeneOrdnerTestbaum("r1-schacht");
        File.WriteAllText(Path.Combine(baum.Extern, "schacht.pdf"), "liegt hinter der Verknüpfung");
        var link = baum.Verknuepfe(baum.Quelle);

        var ergebnis = new ShaftDistributionService().Distribute(new Application.Import.ShaftDistributionRequest(
            new Project(), Path.Combine(baum.Wurzel, "ziel"), PdfSourceFolder: baum.Quelle));

        var treffer = Assert.Single(ergebnis.Items);
        Assert.False(treffer.Success);
        Assert.Equal(UebersprungeneOrdnerTestbaum.ErwarteteZeile(link), treffer.Message);
    }

    private static void AssertFehlerergebnis(
        IReadOnlyList<HoldingFolderDistributor.DistributionResult> ergebnisse,
        string link)
    {
        var treffer = Assert.Single(ergebnisse, r => string.Equals(
            r.Message, UebersprungeneOrdnerTestbaum.ErwarteteZeile(link), StringComparison.Ordinal));
        Assert.False(treffer.Success);
        Assert.Equal(link, treffer.SourcePdfPath);
        Assert.Same(treffer, ergebnisse[0]);
    }
}
