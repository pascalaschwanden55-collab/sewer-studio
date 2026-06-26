using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionStartupDisplayActions(
    Func<LiveDetectionDialogService> CreateDialogs);

public static class LiveDetectionStartupDisplayWorkflow
{
    public static Task<bool> StartAsync(
        Func<AiRuntimeSettings> loadSettings,
        Func<AiRuntimeSettings, Task<LiveDetectionRuntime>> createRuntimeAsync,
        LiveDetectionStartupActions startupActions)
        => StartAsync(
            loadSettings,
            createRuntimeAsync,
            startupActions,
            new LiveDetectionStartupDisplayActions(LiveDetectionDialogServiceFactory.Create));

    public static async Task<bool> StartAsync(
        Func<AiRuntimeSettings> loadSettings,
        Func<AiRuntimeSettings, Task<LiveDetectionRuntime>> createRuntimeAsync,
        LiveDetectionStartupActions startupActions,
        LiveDetectionStartupDisplayActions displayActions)
    {
        ArgumentNullException.ThrowIfNull(loadSettings);
        ArgumentNullException.ThrowIfNull(createRuntimeAsync);
        ArgumentNullException.ThrowIfNull(startupActions);
        ArgumentNullException.ThrowIfNull(displayActions);
        ArgumentNullException.ThrowIfNull(displayActions.CreateDialogs);

        var dialogs = displayActions.CreateDialogs();
        ArgumentNullException.ThrowIfNull(dialogs);

        return await LiveDetectionStartupWorkflow.StartAsync(
            loadSettings,
            createRuntimeAsync,
            dialogs,
            startupActions).ConfigureAwait(true);
    }
}
