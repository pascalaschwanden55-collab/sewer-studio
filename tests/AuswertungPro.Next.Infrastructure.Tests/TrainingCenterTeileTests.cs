using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Paket A (03.10.2026): Direkte Tests der aus <see cref="TrainingCenterImportService"/> ausgelagerten Klassen.
/// Das Verhalten der Fassade belegen die unveraenderten Training-Center-Tests; hier je Klasse ein Kernfall.
/// </summary>
public sealed class TrainingCenterTeileTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-trainingcenter-teile-" + Guid.NewGuid().ToString("N"));

    public TrainingCenterTeileTests()
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
    public void Paarung_waehlt_das_video_mit_passendem_haltungsschluessel_und_schliesst_grafik_aus()
    {
        var passend = Datei("H_24379-41412.mp4", 10);
        Datei("H_99999-88888.mp4", 200);
        var protokoll = Datei("bericht_24379-41412.pdf", 10);

        var (video, proto) = TrainingCenterPaarung.ResolvePair(
            [passend, Path.Combine(_root, "H_99999-88888.mp4")], [protokoll], "24379-41412");

        Assert.Equal(passend, video);
        Assert.Equal(protokoll, proto);
        Assert.True(TrainingCenterPaarung.IstAusgeschlossenesVideo(@"C:\x\Haltung_g.mpg"));
        Assert.False(TrainingCenterPaarung.IstAusgeschlossenesVideo(@"C:\x\Haltung.mpg"));
    }

    [Fact]
    public void ProtokollJson_schreibt_bekannte_codes_im_lesbaren_format()
    {
        var eintraege = TrainingCenterProtokollJson.ExtractEntriesFromChunkText("12.30 BAB Riss laengs  \n3.10 XYZ unbekannt");
        var pfad = Path.Combine(_root, "h_protokoll.json");

        TrainingCenterProtokollJson.WriteProtocolJson(pfad, eintraege, "23021-22369", "1-2");

        using var json = JsonDocument.Parse(File.ReadAllText(pfad));
        Assert.Equal("23021-22369", json.RootElement.GetProperty("HaltungId").GetString());
        var eintrag = Assert.Single(json.RootElement.GetProperty("Current").GetProperty("Entries").EnumerateArray());
        Assert.Equal("BAB", eintrag.GetProperty("Code").GetString());
        Assert.Equal(12.3, eintrag.GetProperty("MeterStart").GetDouble(), 3);
    }

    [Fact]
    public void VideoIndex_ordnet_haltungs_id_zu_und_laesst_grafikvideos_aus()
    {
        var videos = Path.Combine(_root, "Videos");
        Directory.CreateDirectory(videos);
        var video = Path.Combine(videos, "H_23021-22369.mpg");
        File.WriteAllText(video, "video");
        File.WriteAllText(Path.Combine(videos, "H_23022-22370_g.mpg"), "grafik");

        var index = TrainingCenterVideoIndex.BuildVideoIndex(videos, [], CancellationToken.None);

        Assert.Equal(video, Assert.Single(index).Value);
        Assert.Equal("23021-22369", TrainingCenterVideoIndex.NormalizeId(" 23021/22369 "));
    }

    [Fact]
    public void Haltungsverteilung_legt_je_haltung_ordner_und_protokoll_an()
    {
        var ausgabe = Path.Combine(_root, "Sammel_Training");
        var verteilung = new TrainingCenterHaltungsverteilung(
            _ => new PdfTextExtraction([Protokollseite("23021-22369")], ""),
            ordner => Directory.EnumerateFiles(ordner),
            nachHaltungsordner: null,
            leseAttribute: null,
            new TrainingCenterFallDateien(TrainingCenterFallScan.VideoExts, null));

        var ergebnis = verteilung.Verteile(Path.Combine(_root, "Sammel.pdf"), _root, ausgabe, [], CancellationToken.None);

        Assert.Equal(1, ergebnis.Distributed);
        Assert.True(File.Exists(Path.Combine(ausgabe, "23021-22369", "23021-22369_protokoll.json")));
    }

    [Fact]
    public void FallScan_paart_video_und_protokoll_je_ordner()
    {
        var fall = Path.Combine(_root, "24379-41412");
        Directory.CreateDirectory(fall);
        var video = Path.Combine(fall, "H_24379-41412.mp4");
        var protokoll = Path.Combine(fall, "bericht_24379-41412.pdf");
        File.WriteAllText(video, "video");
        File.WriteAllText(protokoll, "pdf");
        var scan = new TrainingCenterFallScan(
            ordner => Directory.EnumerateFiles(ordner),
            new TrainingCenterFallDateien(TrainingCenterFallScan.VideoExts, null));

        var faelle = scan.Scan(_root, null, null, CancellationToken.None);

        var gefunden = Assert.Single(faelle);
        Assert.Equal("24379-41412", gefunden.CaseId);
        Assert.Equal(video, gefunden.VideoPath);
        Assert.Equal(protokoll, gefunden.ProtocolPath);
    }

    private string Datei(string name, int bytes)
    {
        var pfad = Path.Combine(_root, name);
        File.WriteAllBytes(pfad, new byte[bytes]);
        return pfad;
    }

    private static string Protokollseite(string haltung)
        => string.Join("\n",
        [
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Haltungsname:                Datum :                Wetter :               Operator :",
            $" {haltung}                22.04.2014          schoen_trocken           Manuel Joschko"
        ]);
}
