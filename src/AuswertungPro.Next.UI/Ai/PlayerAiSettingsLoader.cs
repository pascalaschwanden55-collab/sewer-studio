using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public static class PlayerAiSettingsLoader
{
    public static AiPlatformSettings LoadPlatformSettings(IAiSettingsProvider? provider = null)
        => (provider ?? new AppSettingsAiSettingsProvider()).Load();

    public static AiRuntimeSettings LoadRuntimeSettings(IAiSettingsProvider? provider = null)
        => LoadPlatformSettings(provider).ToRuntimeSettings();
}
