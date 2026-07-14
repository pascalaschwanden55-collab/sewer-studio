using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoPlaybackControllerTests
{
    [Fact]
    public void Play_ignoriert_null_record()
    {
        var dialogs = new CapturingDialogService();
        var shown = new List<DataPageVideoPlaybackRequest>();
        var controller = CreateController(
            dialogs,
            ensureVideoPath: _ => throw new InvalidOperationException("path should not be requested"),
            showPlayer: call => shown.Add(call));

        controller.Play(null);

        Assert.Empty(shown);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void Play_bricht_bei_leerem_video_pfad_ab()
    {
        var dialogs = new CapturingDialogService();
        var shown = new List<DataPageVideoPlaybackRequest>();
        var record = new HaltungRecord();
        var controller = CreateController(
            dialogs,
            ensureVideoPath: r =>
            {
                Assert.Same(record, r);
                return "";
            },
            buildOverlay: _ => throw new InvalidOperationException("overlay should not be built"),
            showPlayer: call => shown.Add(call));

        controller.Play(record);

        Assert.Empty(shown);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void Play_startet_player_mit_optionen_overlay_und_record()
    {
        var dialogs = new CapturingDialogService();
        var record = new HaltungRecord();
        var options = PlayerWindowOptions.Default with { VideoOutput = "direct3d9" };
        var overlay = new PlayerDamageOverlayData(
            12.5,
            new[] { new DamageMarkerInfo("BAA", "Schaden", 1.2, null, false) });
        var shown = new List<DataPageVideoPlaybackRequest>();
        var controller = CreateController(
            dialogs,
            ensureVideoPath: _ => "C:\\Video\\haltung.mp4",
            getOptions: () => options,
            buildOverlay: r =>
            {
                Assert.Same(record, r);
                return overlay;
            },
            showPlayer: shown.Add);

        controller.Play(record);

        var call = Assert.Single(shown);
        Assert.Equal("C:\\Video\\haltung.mp4", call.Path);
        Assert.Same(options, call.Options);
        Assert.Same(overlay, call.DamageOverlay);
        Assert.Same(record, call.Record);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void Play_meldet_startfehler_mit_logpfad()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            ensureVideoPath: _ => "C:\\Video\\haltung.mp4",
            showPlayer: _ => throw new InvalidOperationException("Start fehlgeschlagen"),
            writeStartErrorLog: (ex, path) =>
            {
                Assert.Equal("Start fehlgeschlagen", ex.Message);
                Assert.Equal("C:\\Video\\haltung.mp4", path);
                return "C:\\Logs\\video.log";
            });

        controller.Play(new HaltungRecord());

        Assert.NotNull(dialogs.LastError);
        Assert.Equal("Video", dialogs.LastError.Value.Title);
        Assert.Contains("Programmlog", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C:\\Logs\\video.log", dialogs.LastError.Value.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Start fehlgeschlagen", dialogs.LastError.Value.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Play_meldet_native_hint_ohne_logpfad()
    {
        var dialogs = new CapturingDialogService();
        var controller = CreateController(
            dialogs,
            ensureVideoPath: _ => "C:\\Video\\haltung.mp4",
            showPlayer: _ => throw new InvalidOperationException("Fehler auf native side"),
            writeStartErrorLog: (_, _) => null);

        controller.Play(new HaltungRecord());

        Assert.NotNull(dialogs.LastError);
        Assert.Equal("Video", dialogs.LastError.Value.Title);
        Assert.Contains("VideoLAN.LibVLC.Windows", dialogs.LastError.Value.Message, StringComparison.Ordinal);
        Assert.Contains("Technische Details konnten nicht gespeichert werden", dialogs.LastError.Value.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("native side", dialogs.LastError.Value.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DataPageVideoPlaybackController CreateController(
        CapturingDialogService dialogs,
        Func<HaltungRecord, string?>? ensureVideoPath = null,
        Func<PlayerWindowOptions>? getOptions = null,
        Func<HaltungRecord, PlayerDamageOverlayData?>? buildOverlay = null,
        Action<DataPageVideoPlaybackRequest>? showPlayer = null,
        Func<Exception, string, string?>? writeStartErrorLog = null)
        => new(
            dialogs,
            ensureVideoPath ?? (_ => "C:\\Video\\haltung.mp4"),
            getOptions ?? (() => PlayerWindowOptions.Default),
            buildOverlay ?? (_ => null),
            call => showPlayer?.Invoke(call),
            writeStartErrorLog ?? ((_, _) => null));

    private sealed class CapturingDialogService : IDialogService
    {
        public (string Message, string Title)? LastError { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => throw new NotSupportedException();

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => throw new NotSupportedException();

        public string[] OpenFiles(string title, string filter)
            => throw new NotSupportedException();

        public string? SelectFolder(string title, string? initialPath = null)
            => throw new NotSupportedException();

        public void Info(string message, string title = "Hinweis")
            => throw new NotSupportedException();

        public void Warn(string message, string title = "Warnung")
            => throw new NotSupportedException();

        public void Error(string message, string title = "Fehler")
            => LastError = (message, title);

        public bool Confirm(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
            => throw new NotSupportedException();

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
            => throw new NotSupportedException();
    }
}
