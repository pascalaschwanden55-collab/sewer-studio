using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Paket B (03.10.2026, Entscheid Pascal): Widersprechen sich die Haltungsschluessel von Video und Protokoll,
/// darf die Paarung einen Teil weiterhin verwerfen – der Grund muss aber als Dateihinweis sichtbar sein
/// (Video, Protokoll, beide Schluessel). Sonst keine Verhaltensaenderung.
/// </summary>
public sealed class TrainingCenterWiderspruchTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-trainingcenter-widerspruch-" + Guid.NewGuid().ToString("N"));

    public TrainingCenterWiderspruchTests()
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
    public async Task Widersprechendes_video_wird_uebersprungen_und_im_hinweis_genannt()
    {
        var fall = Fallordner("24379-41412");
        var video = Datei(fall, "H_99999-88888.mp4");
        var protokoll = Datei(fall, "bericht_24379-41412.pdf");
        Datei(fall, "situationsplan.pdf");
        var hinweise = new List<string>();

        var gefunden = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            _root, null, hinweise, CancellationToken.None));

        Assert.Equal("", gefunden.VideoPath);
        Assert.Equal(protokoll, gefunden.ProtocolPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains($"Video «{video}» (Haltungsschlüssel 99999-88888)", hinweis, StringComparison.Ordinal);
        Assert.Contains($"Protokoll «{protokoll}» (Haltungsschlüssel 24379-41412)", hinweis, StringComparison.Ordinal);
        Assert.Contains("Video nicht verwendet", hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Widersprechendes_protokoll_wird_uebersprungen_und_im_hinweis_genannt()
    {
        var fall = Fallordner("24379-41412");
        var video = Datei(fall, "H_24379-41412.mp4");
        var protokoll = Datei(fall, "bericht_99999-88888.pdf");
        Datei(fall, "situationsplan.pdf");
        var hinweise = new List<string>();

        var gefunden = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            _root, null, hinweise, CancellationToken.None));

        Assert.Equal(video, gefunden.VideoPath);
        Assert.Equal("", gefunden.ProtocolPath);
        var hinweis = Assert.Single(hinweise);
        Assert.Contains("(Haltungsschlüssel 24379-41412)", hinweis, StringComparison.Ordinal);
        Assert.Contains("(Haltungsschlüssel 99999-88888)", hinweis, StringComparison.Ordinal);
        Assert.Contains("Protokoll nicht verwendet", hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passende_haltungsschluessel_ergeben_keinen_hinweis()
    {
        var fall = Fallordner("24379-41412");
        var video = Datei(fall, "H_24379-41412.mp4");
        var protokoll = Datei(fall, "bericht_24379-41412.pdf");
        Datei(fall, "situationsplan.pdf");
        var hinweise = new List<string>();

        var gefunden = Assert.Single(await new TrainingCenterImportService().ScanAsync(
            _root, null, hinweise, CancellationToken.None));

        Assert.Equal(video, gefunden.VideoPath);
        Assert.Equal(protokoll, gefunden.ProtocolPath);
        Assert.Empty(hinweise);
    }

    [Fact]
    public async Task Ohne_hinweisliste_bleibt_das_ergebnis_gleich()
    {
        var fall = Fallordner("24379-41412");
        Datei(fall, "H_99999-88888.mp4");
        var protokoll = Datei(fall, "bericht_24379-41412.pdf");
        Datei(fall, "situationsplan.pdf");

        var gefunden = Assert.Single(await new TrainingCenterImportService().ScanAsync(_root));

        Assert.Equal("", gefunden.VideoPath);
        Assert.Equal(protokoll, gefunden.ProtocolPath);
    }

    private string Fallordner(string name)
    {
        var ordner = Path.Combine(_root, name);
        Directory.CreateDirectory(ordner);
        return ordner;
    }

    private static string Datei(string ordner, string name)
    {
        var pfad = Path.Combine(ordner, name);
        File.WriteAllText(pfad, name);
        return pfad;
    }
}
