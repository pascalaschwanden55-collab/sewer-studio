using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackDialogServiceTests
{
    [Fact]
    public void ShowUnsupportedRate_formats_message_and_title()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new PlayerPlaybackDialogService((message, title) =>
        {
            capturedMessage = message;
            capturedTitle = title;
        });

        service.ShowUnsupportedRate(4f);

        Assert.Equal("SetRate(4) nicht unterst\u00fctzt f\u00fcr dieses Video.", capturedMessage);
        Assert.Equal("Video", capturedTitle);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = PlayerPlaybackDialogServiceFactory.Create();

        Assert.NotNull(service);
    }
}
