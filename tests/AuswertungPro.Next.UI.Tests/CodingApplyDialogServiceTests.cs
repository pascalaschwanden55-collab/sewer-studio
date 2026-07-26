using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingApplyDialogServiceTests
{
    [Fact]
    public void ConfirmEmptyProtocol_skips_dialog_when_guard_does_not_require_confirmation()
    {
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."),
            (_, _) => throw new InvalidOperationException("ConfirmCancel must not be called."));

        var result = service.ConfirmEmptyProtocol(
            new CodingApplyEmptyProtocolGuardResult(
                RequiresConfirmation: false,
                Message: string.Empty,
                Title: string.Empty));

        Assert.True(result);
    }

    [Fact]
    public void ConfirmEmptyProtocol_delegates_required_confirmation()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var service = new CodingApplyDialogService(
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return false;
            },
            (_, _) => throw new InvalidOperationException("ConfirmCancel must not be called."));

        var result = service.ConfirmEmptyProtocol(
            new CodingApplyEmptyProtocolGuardResult(
                RequiresConfirmation: true,
                Message: "Befunde wirklich loeschen?",
                Title: "Leere Codierung"));

        Assert.False(result);
        Assert.Equal("Befunde wirklich loeschen?", capturedMessage);
        Assert.Equal("Leere Codierung", capturedTitle);
    }

    [Fact]
    public void ConfirmUnappliedChangesOnClose_uses_close_prompt_and_policy()
    {
        string? capturedMessage = null;
        string? capturedTitle = null;
        var applyCalled = false;
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."),
            (message, title) =>
            {
                capturedMessage = message;
                capturedTitle = title;
                return DialogConfirm.No;
            });

        var result = service.ConfirmUnappliedChangesOnClose(() =>
        {
            applyCalled = true;
            return false;
        });

        Assert.True(result);
        Assert.False(applyCalled);
        Assert.Contains("nicht \u00fcbernommene Codierungen", capturedMessage);
        Assert.Contains("Ja = \u00fcbernehmen", capturedMessage);
        Assert.Contains("Abbrechen = Fenster offen lassen", capturedMessage);
        Assert.Equal("Codier-Modus", capturedTitle);
    }

    [Fact]
    public void ConfirmUnappliedChangesOnClose_returns_apply_result_for_yes()
    {
        var service = new CodingApplyDialogService(
            (_, _) => throw new InvalidOperationException("ConfirmWarn must not be called."),
            (_, _) => DialogConfirm.Yes);

        var result = service.ConfirmUnappliedChangesOnClose(() => false);

        Assert.False(result);
    }

    [Fact]
    public void Factory_creates_dialog_service()
    {
        var service = CodingApplyDialogServiceFactory.Create();

        Assert.NotNull(service);
    }
}
