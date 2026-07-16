using System.Windows;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert den Vertrag des Ruhe-Schalters: Die ausdrueckliche Einstellung des Nutzers gewinnt,
/// ohne Einstellung gilt die Windows-Vorgabe. Der Schalter darf nur zusaetzlich beruhigen —
/// ein nicht gesetzter Haken darf einem systemweit ruhiggestellten Windows nicht widersprechen.
/// </summary>
public sealed class MotionSettingsTests : IDisposable
{
    public void Dispose() => MotionSettings.ResetForTests();

    [Fact]
    public void Explicit_setting_wins_over_system_value()
    {
        MotionSettings.ReduceMotion = true;
        Assert.True(MotionSettings.ReduceMotion);

        MotionSettings.ReduceMotion = false;
        Assert.False(MotionSettings.ReduceMotion);
    }

    [Fact]
    public void Reset_falls_back_to_the_windows_system_value()
    {
        MotionSettings.ReduceMotion = true;

        MotionSettings.ResetForTests();

        Assert.Equal(!SystemParameters.ClientAreaAnimation, MotionSettings.ReduceMotion);
    }

    [Fact]
    public void Configure_with_true_always_reduces()
    {
        MotionSettings.Configure(true);

        Assert.True(MotionSettings.ReduceMotion);
    }

    [Fact]
    public void Configure_with_false_follows_windows_instead_of_forcing_animations()
    {
        // Der Standardwert der Einstellung ist false. Wuerde er hart als "Animationen an"
        // durchgereicht, verloere ein systemweit ruhiggestelltes Windows seine Wirkung.
        MotionSettings.ReduceMotion = true;

        MotionSettings.Configure(false);

        Assert.Equal(!SystemParameters.ClientAreaAnimation, MotionSettings.ReduceMotion);
    }
}
