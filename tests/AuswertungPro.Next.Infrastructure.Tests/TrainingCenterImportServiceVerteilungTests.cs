using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Deepscan 02.10.2026, R6: Die Haltungsverteilung und der Scan des Training Centers liefen
/// synchron im UI-Thread und ohne Abbruch. Diese Tests halten fest, dass beide Arbeiten
/// ausserhalb des aufrufenden Threads laufen und ein Abbruch je Haltung bzw. Ordner wirkt.
/// </summary>
public sealed class TrainingCenterImportServiceVerteilungTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-trainingcenter-verteilung-" + Guid.NewGuid().ToString("N"));

    public TrainingCenterImportServiceVerteilungTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }

    [Fact]
    public async Task DistributeByHaltungAsync_kehrt_zurueck_bevor_die_pdf_arbeit_endet()
    {
        using var pdfGesperrt = new ManualResetEventSlim(false);
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ =>
            {
                pdfGesperrt.Wait(TimeSpan.FromSeconds(30));
                return new PdfTextExtraction([], "");
            },
            dateienImOrdner: null,
            nachHaltungsordner: null);

        // StartNew statt Task.Run: das Ergebnis ist fertig, sobald der Aufruf zurueckkehrt.
        var aufruf = Task.Factory.StartNew(() => dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"),
            _root,
            Path.Combine(_root, "Sammel_Training")));
        var sofortZurueck = await Task.WhenAny(aufruf, Task.Delay(TimeSpan.FromSeconds(5))) == aufruf;
        pdfGesperrt.Set();

        Assert.True(sofortZurueck, "Die Verteilung blockiert den Aufrufer, bis das PDF gelesen ist (UI-Thread).");
        var ergebnis = await await aufruf;
        Assert.Contains("Kein Text im PDF gefunden.", ergebnis.Messages);
    }

    [Fact]
    public async Task DistributeByHaltungAsync_abbruch_vor_dem_zweiten_chunk_legt_nur_einen_ordner_an()
    {
        using var abbruch = new CancellationTokenSource();
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ => new PdfTextExtraction(
                [Protokollseite("23021-22369"), Protokollseite("23022-22370")],
                ""),
            dateienImOrdner: null,
            nachHaltungsordner: _ => abbruch.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"),
            _root,
            ausgabe,
            abbruch.Token));

        var ordner = Assert.Single(Directory.GetDirectories(ausgabe));
        Assert.Equal("23021-22369", Path.GetFileName(ordner));
    }

    // Review PR #85: Ein Abbruch waehrend der LETZTEN Haltung darf nicht als erfolgreiche Verteilung enden.
    [Fact]
    public async Task DistributeByHaltungAsync_abbruch_in_der_letzten_haltung_endet_als_abbruch()
    {
        using var abbruch = new CancellationTokenSource();
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ => new PdfTextExtraction([Protokollseite("23021-22369")], ""),
            dateienImOrdner: null,
            nachHaltungsordner: _ => abbruch.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"),
            _root,
            Path.Combine(_root, "Sammel_Training"),
            abbruch.Token));
    }

    [Fact]
    public async Task DistributeByHaltungAsync_ohne_abbruch_legt_beide_ordner_an()
    {
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: _ => new PdfTextExtraction(
                [Protokollseite("23021-22369"), Protokollseite("23022-22370")],
                ""),
            dateienImOrdner: null,
            nachHaltungsordner: null);

        var ergebnis = await dienst.DistributeByHaltungAsync(
            Path.Combine(_root, "Sammel.pdf"),
            _root,
            ausgabe,
            CancellationToken.None);

        Assert.Equal(2, ergebnis.Distributed);
        Assert.Equal(2, Directory.GetDirectories(ausgabe).Length);
    }

    [Fact]
    public async Task ScanAsync_kehrt_zurueck_bevor_die_ordnerarbeit_endet()
    {
        using var ordnerGesperrt = new ManualResetEventSlim(false);
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: null,
            dateienImOrdner: _ =>
            {
                ordnerGesperrt.Wait(TimeSpan.FromSeconds(30));
                return [];
            },
            nachHaltungsordner: null);

        var aufruf = Task.Factory.StartNew(() => dienst.ScanAsync(_root, null, CancellationToken.None));
        var sofortZurueck = await Task.WhenAny(aufruf, Task.Delay(TimeSpan.FromSeconds(5))) == aufruf;
        ordnerGesperrt.Set();

        Assert.True(sofortZurueck, "Der Scan blockiert den Aufrufer, bis alle Ordner gelesen sind (UI-Thread).");
        Assert.Empty(await await aufruf);
    }

    [Fact]
    public async Task ScanAsync_abbruch_nach_dem_ersten_ordner_liest_keinen_weiteren()
    {
        Directory.CreateDirectory(Path.Combine(_root, "A"));
        Directory.CreateDirectory(Path.Combine(_root, "B"));
        using var abbruch = new CancellationTokenSource();
        var gelesen = new List<string>();
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: null,
            dateienImOrdner: ordner =>
            {
                gelesen.Add(ordner);
                abbruch.Cancel();
                return [];
            },
            nachHaltungsordner: null);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dienst.ScanAsync(_root, null, abbruch.Token));

        Assert.Single(gelesen);
    }

    [Fact]
    public async Task ScanAsync_unlesbarer_ordner_wird_gezaehlt_und_benannt_statt_still_ausgelassen()
    {
        var lesbar = Path.Combine(_root, "A");
        var unlesbar = Path.Combine(_root, "B");
        Directory.CreateDirectory(lesbar);
        Directory.CreateDirectory(unlesbar);
        File.WriteAllText(Path.Combine(lesbar, "bericht_24379-41412.pdf"), "pdf");
        var dienst = new TrainingCenterImportService(
            pdfSeitenLesen: null,
            dateienImOrdner: ordner => string.Equals(ordner, unlesbar, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("gesperrt")
                : Directory.EnumerateFiles(ordner),
            nachHaltungsordner: null);
        var uebersprungen = new List<string>();

        var faelle = await dienst.ScanAsync(_root, uebersprungen, CancellationToken.None);

        Assert.Equal(lesbar, Assert.Single(faelle).FolderPath);
        Assert.Equal([unlesbar], uebersprungen);
    }

    private static string Protokollseite(string haltung)
        => string.Join("\n",
        [
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Haltungsname:                Datum :                Wetter :               Operator :",
            $" {haltung}                22.04.2014          schoen_trocken           Manuel Joschko"
        ]);
}
