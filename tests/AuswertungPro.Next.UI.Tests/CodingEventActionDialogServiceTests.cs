using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventActionDialogServiceTests
{
    [Fact]
    public void ShowStretchCloseRequiresLaterMeter_uses_stretch_damage_message()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingEventActionDialogService(
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
            },
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."));

        service.ShowStretchCloseRequiresLaterMeter();

        Assert.Equal(
            "Der aktuelle Meterstand muss gr\u00f6\u00dfer sein als der Anfang des Streckenschadens.",
            capturedMessage);
        Assert.Equal("Streckenschaden", capturedTitle);
    }

    [Fact]
    public void ConfirmDelete_formats_event_code_and_returns_confirmation()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingEventActionDialogService(
            (_, _) => throw new InvalidOperationException("Info must not be called."),
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return true;
            });

        var confirmed = service.ConfirmDelete("BAJ");

        Assert.True(confirmed);
        Assert.Equal("Ereignis 'BAJ' l\u00f6schen?", capturedMessage);
        Assert.Equal("L\u00f6schen", capturedTitle);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = CodingEventActionDialogServiceFactory.Create();

        Assert.NotNull(service);
    }
}
