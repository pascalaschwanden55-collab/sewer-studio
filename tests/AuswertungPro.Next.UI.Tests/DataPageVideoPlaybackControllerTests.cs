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
    public void PlayCounterInspection_ignoriert_null_ohne_pfadaufloesung()
    {
        var dialogs = new CapturingDialogService();
        var shown = new List<DataPageVideoPlaybackRequest>();
        var controller = CreateController(dialogs, showPlayer: shown.Add);

        controller.PlayCounterInspection(
            null,
            _ => throw new InvalidOperationException("path should not be resolved"));

        Assert.Empty(shown);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PlayCounterInspection_meldet_fehlenden_aufgeloesten_pfad(string? resolvedPath)
    {
        var dialogs = new CapturingDialogService();
        var shown = new List<DataPageVideoPlaybackRequest>();
        var record = new HaltungRecord();
        record.SetFieldValue(
            "Link_G",
            @"Video\gegeninspektion.mp4",
            FieldSource.Manual,
            userEdited: true);
        var controller = CreateController(
            dialogs,
            getOptions: () => throw new InvalidOperationException("options should not be requested"),
            buildOverlay: _ => throw new InvalidOperationException("overlay should not be built"),
            showPlayer: shown.Add);

        controller.PlayCounterInspection(
            record,
            rawPath =>
            {
                Assert.Equal(@"Video\gegeninspektion.mp4", rawPath);
                return resolvedPath;
            });

        Assert.Empty(shown);
        Assert.Equal(
            ("Für diese Haltung ist keine Gegeninspektion vorhanden.", "Gegeninspektion"),
            dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void PlayCounterInspection_startet_player_mit_spaet_aufgeloestem_link_g()
    {
        var dialogs = new CapturingDialogService();
        var record = new HaltungRecord();
        record.SetFieldValue(
            "Link_G",
            @"Video\gegeninspektion.mp4",
            FieldSource.Manual,
            userEdited: true);
        var options = PlayerWindowOptions.Default with { VideoOutput = "direct3d9" };
        var overlay = new PlayerDamageOverlayData(15, []);
        var shown = new List<DataPageVideoPlaybackRequest>();
        var controller = CreateController(
            dialogs,
            getOptions: () => options,
            buildOverlay: _ => overlay,
            showPlayer: shown.Add);

        controller.PlayCounterInspection(
            record,
            rawPath =>
            {
                Assert.Equal(@"Video\gegeninspektion.mp4", rawPath);
                return @"C:\Projekt\Video\gegeninspektion.mp4";
            });

        var call = Assert.Single(shown);
        Assert.Equal(@"C:\Projekt\Video\gegeninspektion.mp4", call.Path);
        Assert.Same(options, call.Options);
        Assert.Same(overlay, call.DamageOverlay);
        Assert.Same(record, call.Record);
        Assert.Null(dialogs.LastInfo);
        Assert.Null(dialogs.LastError);
    }

    [Fact]
    public void PlayCounterInspection_laesst_pfadaufloesungsfehler_durch()
    {
        var dialogs = new CapturingDialogService();
        var record = new HaltungRecord();
        record.SetFieldValue(
            "Link_G",
            @"Video\gegeninspektion.mp4",
            FieldSource.Manual,
            userEdited: true);
        var expected = new InvalidOperationException("Pfadauflösung fehlgeschlagen");
        var controller = CreateController(
            dialogs,
            getOptions: () => throw new InvalidOperationException("options should not be requested"),
            showPlayer: _ => throw new InvalidOperationException("player should not be shown"),
            writeStartErrorLog: (_, _) =>
                throw new InvalidOperationException("resolver errors should not be logged as start errors"));

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            controller.PlayCounterInspection(record, _ => throw expected));

        Assert.Same(expected, thrown);
        Assert.Null(dialogs.LastInfo);
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
        public (string Message, string Title)? LastInfo { get; private set; }

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
            => LastInfo = (message, title);

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
