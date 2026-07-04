using System;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

public sealed record SettingsThemeWorkflowUi(
    Func<bool> GetIsSyncing,
    Action<bool> SetIsSyncing,
    Func<string> GetUiTheme,
    Action<string> SetUiTheme,
    Func<bool> GetIsDarkTheme,
    Action<bool> SetIsDarkTheme);

public static class SettingsThemeWorkflow
{
    public static void SyncUiThemeChanged(string value, SettingsThemeWorkflowUi ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.GetIsSyncing())
            return;

        ui.SetIsSyncing(true);
        try
        {
            var normalized = ThemeManager.NormalizeTheme(value);
            if (!string.Equals(normalized, value, StringComparison.Ordinal))
            {
                ui.SetUiTheme(normalized);
                return;
            }

            var shouldBeDark = string.Equals(normalized, ThemeManager.Dark, StringComparison.Ordinal);
            if (ui.GetIsDarkTheme() != shouldBeDark)
                ui.SetIsDarkTheme(shouldBeDark);
        }
        finally
        {
            ui.SetIsSyncing(false);
        }
    }

    public static void SyncIsDarkThemeChanged(bool isDarkTheme, SettingsThemeWorkflowUi ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        if (ui.GetIsSyncing())
            return;

        ui.SetIsSyncing(true);
        try
        {
            var targetTheme = isDarkTheme ? ThemeManager.Dark : ThemeManager.Light;
            if (!string.Equals(ui.GetUiTheme(), targetTheme, StringComparison.Ordinal))
                ui.SetUiTheme(targetTheme);
        }
        finally
        {
            ui.SetIsSyncing(false);
        }
    }

    public static void ApplyTheme(
        AppSettings settings,
        string? uiTheme,
        Action saveSettingsImmediate)
        => ApplyTheme(settings, uiTheme, saveSettingsImmediate, ApplyToApplicationResources);

    public static void ApplyTheme(
        AppSettings settings,
        string? uiTheme,
        Action saveSettingsImmediate,
        Action<string> applyToResources)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(saveSettingsImmediate);
        ArgumentNullException.ThrowIfNull(applyToResources);

        var normalized = ThemeManager.NormalizeTheme(uiTheme);
        settings.UiTheme = normalized;
        saveSettingsImmediate();
        applyToResources(normalized);
    }

    private static void ApplyToApplicationResources(string theme)
    {
        var app = System.Windows.Application.Current;
        if (app != null)
            ThemeManager.ApplyTheme(app.Resources, theme);
    }
}
