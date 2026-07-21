using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoPlaybackArchitectureTests
{
    [Fact]
    public void Counter_inspection_playback_logic_lives_in_video_controller()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));
        var controller = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "DataPageVideoPlaybackController.cs"));
        var compactViewModel = string.Concat(
            viewModel.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains(
            "privatevoidPlayGegenVideo(HaltungRecord?record)=>" +
            "_videoPlaybackController.PlayCounterInspection(record,ResolveExistingPath);",
            compactViewModel);
        Assert.Contains(
            "PlayGegenVideoCommand=newRelayCommand<HaltungRecord?>(PlayGegenVideo);",
            compactViewModel);
        Assert.DoesNotContain("GetFieldValue(\"Link_G\")", viewModel);
        Assert.DoesNotContain("keine Gegeninspektion vorhanden", viewModel);
        Assert.Contains("record.GetFieldValue(\"Link_G\")", controller);
        Assert.Contains("_dialogs.Info(", controller);
        Assert.Contains("PlayResolved(record, path);", controller);
    }
}
