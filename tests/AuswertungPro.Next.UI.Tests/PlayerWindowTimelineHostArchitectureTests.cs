using System.IO;
using static AuswertungPro.Next.UI.Tests.ArchitectureSourceGuard;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowTimelineHostArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_osd_reads_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var hostPath = Path.Combine(uiRoot, "Player", "PlayerTimelineHost.cs");
        var osdPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs");
        var readingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var mediaHostFactoryPath = Path.Combine(uiRoot, "Player", "PlayerMediaHostFactory.cs");

        Assert.True(File.Exists(hostPath), "Player-Zeit/Dauer soll ueber einen PlayerTimelineHost gelesen werden.");
        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");

        var host = File.ReadAllText(hostPath);
        var osd = File.ReadAllText(osdPath);
        var reading = File.ReadAllText(readingPath);
        var state = File.ReadAllText(statePath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var mediaHostFactory = File.ReadAllText(mediaHostFactoryPath);

        Assert.Contains("public sealed class PlayerTimelineHost", host);
        Assert.Contains("double? CurrentSeconds", host);
        Assert.Contains("double? DurationSeconds", host);
        Assert.Contains("private PlayerTimelineHost _playerTimelineHost => _playerMediaHosts.TimelineHost", state);
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("new PlayerTimelineHost", mediaHostFactory);
        Assert.Contains("_playerTimelineHost", osd);
        Assert.Contains("_playerTimelineHost", reading);
    }

    [Fact]
    public void PlayerWindow_coding_event_and_ai_partials_read_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var windowsRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.Coding.Ai.cs",
            "PlayerWindow.Coding.AiEvents.cs",
            "PlayerWindow.Coding.AiEvents.Live.cs",
            "PlayerWindow.Coding.AiEvents.MultiModel.cs",
            "PlayerWindow.Coding.Ai.Streckenschaden.cs",
            "PlayerWindow.Coding.Events.cs",
            "PlayerWindow.Coding.Events.Actions.cs",
            "PlayerWindow.Coding.FrameReadiness.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
        }

        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        Assert.Contains("FallbackVideoTime: () => _playerTimelineHost.CurrentTimeOrZero", windowRoot);
        Assert.Contains("SeekMilliseconds: _playerTimelineHost.SeekMilliseconds", windowRoot);
    }

    [Fact]
    public void PlayerWindow_remaining_coding_timeline_partials_read_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.xaml.cs",
            "PlayerWindow.Coding.Photos.Capture.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
        }

        var navigationController = File.ReadAllText(
            Path.Combine(uiRoot, "Player", "CodingNavigationController.cs"));
        Assert.Contains("_timelineHost", navigationController);
    }

    [Fact]
    public void PlayerWindow_live_detection_marking_reads_player_timeline_through_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var paths = new[]
        {
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.Marking.Catalog.cs"
        };

        foreach (var fileName in paths)
        {
            var path = Path.Combine(windowsRoot, fileName);
            Assert.True(File.Exists(path), $"{fileName} muss als PlayerWindow-Partial existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("_playerTimelineHost", text);
        }

        var confirmationController = File.ReadAllText(
            Path.Combine(uiRoot, "Player", "LiveDetectionConfirmationTrainingController.cs"));
        Assert.Contains("PlayerTimelineHost", confirmationController);
        Assert.Contains("_timelineHost", confirmationController);

        var guardedFiles = new[]
        {
            "PlayerWindow.Coding.Osd.cs",
            "PlayerWindow.Coding.Osd.Reading.cs",
            "PlayerWindow.Coding.Ai.cs",
            "PlayerWindow.Coding.AiEvents.cs",
            "PlayerWindow.Coding.AiEvents.Live.cs",
            "PlayerWindow.Coding.AiEvents.MultiModel.cs",
            "PlayerWindow.Coding.Ai.Streckenschaden.cs",
            "PlayerWindow.Coding.Events.cs",
            "PlayerWindow.Coding.Events.Actions.cs",
            "PlayerWindow.Coding.FrameReadiness.cs",
            "PlayerWindow.Coding.Navigation.cs",
            "PlayerWindow.xaml.cs",
            "PlayerWindow.Coding.Photos.Capture.cs",
            "PlayerWindow.LiveDetection.Confirmation.cs",
            "PlayerWindow.LiveDetection.Confirmation.Training.cs",
            "PlayerWindow.LiveDetection.Marking.cs",
            "PlayerWindow.LiveDetection.Marking.Catalog.cs"
        };

        var offenders = guardedFiles
            .SelectMany(file => FindFileTokenOffenders(
                Path.Combine(windowsRoot, file),
                "_player.Time",
                "_player.Length",
                "_player?.Time",
                "_player?.Length"))
            .Concat(FindFileTokenOffenders(
                Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.cs"),
                "_player.",
                "_player?."))
            .Concat(FindFileTokenOffenders(
                Path.Combine(windowsRoot, "PlayerWindow.Coding.Osd.Reading.cs"),
                "_player.",
                "_player?."))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "PlayerWindow-Timeline-Leser sollen Zeit/Dauer ueber PlayerTimelineHost statt roh ueber _player lesen:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Player_timeline_overlay_controllers_seek_through_timeline_host()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerRoot = Path.Combine(uiRoot, "Player");
        var windowRoot = File.ReadAllText(Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs"));
        var mediaHostFactoryPath = Path.Combine(playerRoot, "PlayerMediaHostFactory.cs");
        var paths = new[]
        {
            Path.Combine(playerRoot, "DamageMarkerController.cs"),
            Path.Combine(playerRoot, "QuickScanController.cs")
        };

        Assert.True(File.Exists(mediaHostFactoryPath), "Player-Hosts sollen gebuendelt ausserhalb des PlayerWindow-Konstruktors verdrahtet werden.");
        Assert.Contains("PlayerMediaRuntimeFactory.Create", windowRoot);
        Assert.Contains("_playerTimelineHost,", windowRoot);
        Assert.Contains("_playerPlaybackControlHost,", windowRoot);

        foreach (var path in paths)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} muss existieren.");

            var text = File.ReadAllText(path);
            Assert.Contains("PlayerTimelineHost", text);
            Assert.Contains("PlayerPlaybackControlHost", text);
        }

        var offenders = paths
            .SelectMany(path => FindFileTokenOffenders(
                path,
                "MediaPlayer",
                "_player.SetPause",
                "_player.Time",
                "_player.Length",
                "_player?.Time",
                "_player?.Length"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Timeline-Overlay-Controller sollen ueber PlayerTimelineHost/PlayerPlaybackControlHost statt MediaPlayer arbeiten:\n"
            + string.Join("\n", offenders));
    }
}
