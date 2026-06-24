using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionStartupActions(
    Action UncheckToggle,
    Action<LiveDetectionRuntime> StartRuntime);

public static class LiveDetectionStartupWorkflow
{
    public static async Task<bool> StartAsync(
        Func<AiRuntimeSettings> loadSettings,
        Func<AiRuntimeSettings, Task<LiveDetectionRuntime>> createRuntimeAsync,
        LiveDetectionDialogService dialogs,
        LiveDetectionStartupActions actions)
    {
        ArgumentNullException.ThrowIfNull(loadSettings);
        ArgumentNullException.ThrowIfNull(createRuntimeAsync);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(actions);

        AiRuntimeSettings settings;
        try
        {
            settings = loadSettings();
        }
        catch
        {
            dialogs.ShowRuntimeSettingsLoadFailed();
            actions.UncheckToggle();
            return false;
        }

        if (!settings.Enabled)
        {
            dialogs.ShowDisabled();
            actions.UncheckToggle();
            return false;
        }

        try
        {
            var runtime = await createRuntimeAsync(settings).ConfigureAwait(true);
            actions.StartRuntime(runtime);
            return true;
        }
        catch (Exception ex)
        {
            actions.UncheckToggle();
            dialogs.ShowStartFailed(ex.Message);
            return false;
        }
    }
}
