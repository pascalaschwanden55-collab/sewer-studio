using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionStartupDisplayWorkflowTests
{
    [Fact]
    public async Task StartAsync_creates_dialog_service_and_delegates_startup_order()
    {
        var calls = new List<string>();

        var result = await LiveDetectionStartupDisplayWorkflow.StartAsync(
            loadSettings: () =>
            {
                calls.Add("load");
                throw new InvalidOperationException("settings");
            },
            createRuntimeAsync: _ => throw new InvalidOperationException("should not create runtime"),
            startupActions: new LiveDetectionStartupActions(
                UncheckToggle: () => calls.Add("uncheck"),
                StartRuntime: _ => throw new InvalidOperationException("should not start runtime")),
            displayActions: new LiveDetectionStartupDisplayActions(
                CreateDialogs: () =>
                {
                    calls.Add("dialogs");
                    return new LiveDetectionDialogService(
                        (message, _) => calls.Add($"warn:{message}"),
                        (message, _) => calls.Add($"info:{message}"));
                }));

        Assert.False(result);
        Assert.Equal(
            [
                "dialogs",
                "load",
                "warn:KI-Konfiguration konnte nicht geladen werden.",
                "uncheck"
            ],
            calls);
    }
}
