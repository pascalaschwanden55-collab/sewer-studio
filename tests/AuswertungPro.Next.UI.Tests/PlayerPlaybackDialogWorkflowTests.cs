using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackDialogWorkflowTests
{
    [Fact]
    public void ShowUnsupportedRate_creates_service_and_shows_dialog()
    {
        var calls = new List<string>();
        var service = new PlayerPlaybackDialogService((message, title) =>
            calls.Add($"{message}:{title}"));

        PlayerPlaybackDialogWorkflow.ShowUnsupportedRate(
            4f,
            new PlayerPlaybackDialogWorkflowActions(
                CreateDialogService: () =>
                {
                    calls.Add("service");
                    return service;
                }));

        Assert.Equal(["service", "SetRate(4) nicht unterst\u00fctzt f\u00fcr dieses Video.:Video"], calls);
    }
}
