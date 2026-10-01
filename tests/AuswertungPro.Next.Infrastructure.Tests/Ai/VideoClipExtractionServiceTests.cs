using System.Diagnostics;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai;

/// <summary>
/// Die drei Haerten des Clip-Schneiders — dieselben, die der Bildfolgen-Extraktor
/// hat: woertliche ffmpeg-Fehlerausgabe, kein Ergebnis ist ein Fehler, nie eine
/// Alt-Datei unterschieben.
/// </summary>
public sealed class VideoClipExtractionServiceTests : IDisposable
{
    private readonly string _video;
    private readonly string _ffmpeg;

    public VideoClipExtractionServiceTests()
    {
        _video = Path.Combine(Path.GetTempPath(), $"clip-src-{Guid.NewGuid():N}.mp4");
        _ffmpeg = Path.Combine(Path.GetTempPath(), $"ffmpeg-{Guid.NewGuid():N}.exe");
        File.WriteAllText(_video, "video");
        File.WriteAllText(_ffmpeg, "attrappe");
    }

    public void Dispose()
    {
        if (File.Exists(_video))
            File.Delete(_video);
        if (File.Exists(_ffmpeg))
            File.Delete(_ffmpeg);
    }

    [Fact]
    public async Task Ein_ffmpeg_Fehlschlag_reicht_die_Ausgabe_woertlich_durch()
    {
        var dienst = new VideoClipExtractionService(
            new FesterProzess(new ProcessOutputResult(1, "", "moov atom not found")));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dienst.CutClipAsync(_ffmpeg, _video,
                TimeSpan.Zero, TimeSpan.FromSeconds(5), CancellationToken.None));

        Assert.Contains("ffmpeg ist fehlgeschlagen", fehler.Message);
        Assert.Contains("moov atom not found", fehler.Message);
    }

    [Fact]
    public async Task Ein_Lauf_ohne_Ergebnisdatei_ist_ein_Fehler_kein_leerer_Clip()
    {
        // Exit 0, aber keine Datei geschrieben — ein defektes Video darf nicht
        // wie eine leere Stelle aussehen.
        var dienst = new VideoClipExtractionService(
            new FesterProzess(new ProcessOutputResult(0, "", "")));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dienst.CutClipAsync(_ffmpeg, _video,
                TimeSpan.Zero, TimeSpan.FromSeconds(5), CancellationToken.None));

        Assert.Contains("keinen Clip erzeugt", fehler.Message);
    }

    [Fact]
    public async Task Ein_leeres_Ergebnis_ist_ein_Fehler()
    {
        var dienst = new VideoClipExtractionService(
            new FesterProzess(new ProcessOutputResult(0, "", ""), schreibeLeereDatei: true));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dienst.CutClipAsync(_ffmpeg, _video,
                TimeSpan.Zero, TimeSpan.FromSeconds(5), CancellationToken.None));

        Assert.Contains("keinen Clip erzeugt", fehler.Message);
    }

    [Fact]
    public async Task Ein_fehlendes_Video_ist_ein_Fehler_vor_jedem_Prozessstart()
    {
        var prozess = new FesterProzess(new ProcessOutputResult(0, "", ""));
        var dienst = new VideoClipExtractionService(prozess);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => dienst.CutClipAsync("ffmpeg.exe", @"D:\gibt-es-nicht.mpg",
                TimeSpan.Zero, TimeSpan.FromSeconds(5), CancellationToken.None));

        Assert.False(prozess.Gestartet);
    }

    [Fact]
    public async Task Ein_Erfolg_liefert_den_Clip_Pfad()
    {
        var dienst = new VideoClipExtractionService(
            new FesterProzess(new ProcessOutputResult(0, "", ""), schreibeInhalt: true));

        var pfad = await dienst.CutClipAsync(_ffmpeg, _video,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(9), CancellationToken.None);

        Assert.True(File.Exists(pfad));
        File.Delete(pfad);
    }

    /// <summary>Nachbildung des Prozesslesers; kann das Ziel wie ffmpeg schreiben.</summary>
    private sealed class FesterProzess : IProcessOutputReader
    {
        private readonly ProcessOutputResult _ergebnis;
        private readonly bool _schreibeLeereDatei;
        private readonly bool _schreibeInhalt;

        internal FesterProzess(
            ProcessOutputResult ergebnis, bool schreibeLeereDatei = false, bool schreibeInhalt = false)
        {
            _ergebnis = ergebnis;
            _schreibeLeereDatei = schreibeLeereDatei;
            _schreibeInhalt = schreibeInhalt;
        }

        public bool Gestartet { get; private set; }

        public Task<ProcessOutputResult?> ReadToExitAsync(
            ProcessStartInfo startInfo,
            CancellationToken cancellationToken,
            Action<int>? onStarted = null)
        {
            Gestartet = true;
            if (_schreibeLeereDatei || _schreibeInhalt)
            {
                // Der Zielpfad steht als letztes Argument in Anfuehrungszeichen.
                var teile = startInfo.Arguments.Split('"');
                var ziel = teile[^2];
                File.WriteAllText(ziel, _schreibeInhalt ? "clipdaten" : string.Empty);
            }
            return Task.FromResult<ProcessOutputResult?>(_ergebnis);
        }
    }
}
