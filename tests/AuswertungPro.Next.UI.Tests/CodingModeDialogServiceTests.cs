using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeDialogServiceTests
{
    [Fact]
    public void ShowMissingHaltung_uses_coding_mode_info()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowMissingHaltung();

        var call = calls.Single();
        Assert.Equal("info", call.Kind);
        Assert.Contains("Codier-Modus ben\u00f6tigt eine Haltung.", call.Message);
        Assert.Equal("Codier-Modus", call.Title);
    }

    [Fact]
    public void ShowSessionStartFailed_uses_coding_mode_warning()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowSessionStartFailed("Laenge fehlt");

        Assert.Equal(("warn", "Laenge fehlt", "Codier-Modus"), calls.Single());
    }

    [Fact]
    public void ShowImportFrameCaptureFailed_uses_import_confirmation_warning()
    {
        var calls = new List<(string Kind, string Message, string Title)>();
        var service = Service(calls);

        service.ShowImportFrameCaptureFailed();

        var call = calls.Single();
        Assert.Equal("warn", call.Kind);
        Assert.Contains("Frame konnte nicht aufgenommen werden.", call.Message);
        Assert.Equal("Import bestaetigen", call.Title);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = CodingModeDialogServiceFactory.Create();

        Assert.NotNull(service);
    }

    private static CodingModeDialogService Service(List<(string Kind, string Message, string Title)> calls)
        => new(
            (message, title) => calls.Add(("info", message, title)),
            (message, title) => calls.Add(("warn", message, title)));
}
