using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionDialogServiceTests
{
    [Fact]
    public void ShowRuntimeSettingsLoadFailed_uses_live_ki_warning()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowRuntimeSettingsLoadFailed();

        Assert.Equal(("warn", "KI-Konfiguration konnte nicht geladen werden.", "Live-KI"), calls.Single());
    }

    [Fact]
    public void ShowDisabled_uses_live_ki_info()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowDisabled();

        Assert.Equal(("info", "KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Live-KI"), calls.Single());
    }

    [Fact]
    public void ShowStartFailed_includes_error_message()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowStartFailed("Port belegt");

        Assert.Equal(("warn", "Live-KI konnte nicht gestartet werden: Port belegt", "Live-KI"), calls.Single());
    }

    [Fact]
    public void ShowCodeCatalogUnavailable_uses_marking_info()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowCodeCatalogUnavailable();

        var call = calls.Single();
        Assert.Equal("info", call.Kind);
        Assert.Contains("Schadenscode-Katalog nicht verf\u00fcgbar.", call.Message);
        Assert.Equal("Markieren", call.Title);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = LiveDetectionDialogServiceFactory.Create();

        Assert.NotNull(service);
    }

    private static LiveDetectionDialogService Service(List<(string Kind, string Message, string Title)> calls)
        => new(
            (message, title) => calls.Add(("warn", message, title)),
            (message, title) => calls.Add(("info", message, title)));
}
