using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsThemeWorkflowTests
{
    [Fact]
    public void SyncUiThemeChanged_dark_sets_dark_toggle()
    {
        var state = new ThemeState { UiTheme = ThemeManager.Dark, IsDarkTheme = false };

        SettingsThemeWorkflow.SyncUiThemeChanged(
            ThemeManager.Dark,
            Ui(state));

        Assert.Equal(ThemeManager.Dark, state.UiTheme);
        Assert.True(state.IsDarkTheme);
        Assert.False(state.IsSyncing);
    }

    [Fact]
    public void SyncUiThemeChanged_invalid_theme_normalizes_theme_name()
    {
        var state = new ThemeState { UiTheme = "Sepia", IsDarkTheme = false };

        SettingsThemeWorkflow.SyncUiThemeChanged(
            "Sepia",
            Ui(state));

        Assert.Equal(ThemeManager.Light, state.UiTheme);
        Assert.False(state.IsDarkTheme);
        Assert.False(state.IsSyncing);
    }

    [Fact]
    public void SyncIsDarkThemeChanged_true_sets_ui_theme_to_dark()
    {
        var state = new ThemeState { UiTheme = ThemeManager.Light, IsDarkTheme = true };

        SettingsThemeWorkflow.SyncIsDarkThemeChanged(
            isDarkTheme: true,
            Ui(state));

        Assert.Equal(ThemeManager.Dark, state.UiTheme);
        Assert.True(state.IsDarkTheme);
        Assert.False(state.IsSyncing);
    }

    [Fact]
    public void SyncUiThemeChanged_while_syncing_does_nothing()
    {
        var state = new ThemeState
        {
            UiTheme = ThemeManager.Light,
            IsDarkTheme = false,
            IsSyncing = true
        };

        SettingsThemeWorkflow.SyncUiThemeChanged(
            ThemeManager.Dark,
            Ui(state));

        Assert.Equal(ThemeManager.Light, state.UiTheme);
        Assert.False(state.IsDarkTheme);
        Assert.True(state.IsSyncing);
    }

    [Fact]
    public void ApplyTheme_normalizes_saves_and_applies_theme()
    {
        var settings = new AppSettings { UiTheme = ThemeManager.Light };
        var calls = new List<string>();

        SettingsThemeWorkflow.ApplyTheme(
            settings,
            "dark",
            saveSettingsImmediate: () => calls.Add("save"),
            applyToResources: theme => calls.Add("apply:" + theme));

        Assert.Equal(ThemeManager.Dark, settings.UiTheme);
        Assert.Equal(["save", "apply:Dark"], calls);
    }

    private static SettingsThemeWorkflowUi Ui(ThemeState state)
        => new(
            GetIsSyncing: () => state.IsSyncing,
            SetIsSyncing: value => state.IsSyncing = value,
            GetUiTheme: () => state.UiTheme,
            SetUiTheme: value => state.UiTheme = value,
            GetIsDarkTheme: () => state.IsDarkTheme,
            SetIsDarkTheme: value => state.IsDarkTheme = value);

    private sealed class ThemeState
    {
        public string UiTheme { get; set; } = ThemeManager.Light;
        public bool IsDarkTheme { get; set; }
        public bool IsSyncing { get; set; }
    }
}
