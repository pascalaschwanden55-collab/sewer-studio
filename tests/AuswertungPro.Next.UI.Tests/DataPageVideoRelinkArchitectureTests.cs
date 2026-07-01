using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoRelinkArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_video_relink_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethodBody(source, "private void RelinkVideo(HaltungRecord? record)");

        Assert.Contains("_videoRelinkController.Relink(record);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("MediaFileTypes.VideoDialogFilter", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.OpenFile(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("LastVideoSourceFolder", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveVideoLink(", method, StringComparison.Ordinal);
    }

}
