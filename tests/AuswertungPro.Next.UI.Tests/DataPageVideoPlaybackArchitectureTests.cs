using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoPlaybackArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_video_playback_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethodBody(source, "private void PlayVideo(HaltungRecord? record)");

        Assert.Contains("_videoPlaybackController.Play(record);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new PlayerWindow", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageVideoOverlayBuilder.Build", method, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageVideoStartErrorLogWriter.TryWrite", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Error", method, StringComparison.Ordinal);
    }

}
