using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Services;

public static class WindowBackdropHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtNone = 1;
    private const int DwmsbtMainWindow = 2;

    public static bool IsMicaSupported(Version osVersion)
        => osVersion.Major > 10 || osVersion.Major == 10 && osVersion.Build >= 22621;

    public static bool IsDarkTitleBarSupported(Version osVersion)
        => osVersion.Major > 10 || osVersion.Major == 10 && osVersion.Build >= 17763;

    public static void Apply(Window window, string? theme, bool useMica)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var osVersion = Environment.OSVersion.Version;
        ApplyDarkTitleBar(hwnd, theme, osVersion);
        ApplyMica(window, hwnd, useMica, osVersion);
    }

    public static void ApplyToOpenWindows(string? theme)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        foreach (Window window in app.Windows)
        {
            Apply(window, theme, Fluent.GetBackdrop(window) == FluentBackdrop.Mica);
        }
    }

    private static void ApplyDarkTitleBar(IntPtr hwnd, string? theme, Version osVersion)
    {
        if (!IsDarkTitleBarSupported(osVersion))
            return;

        var darkMode = string.Equals(ThemeManager.NormalizeTheme(theme), ThemeManager.Dark, StringComparison.Ordinal)
            ? 1
            : 0;
        TrySetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, darkMode);
    }

    private static void ApplyMica(Window window, IntPtr hwnd, bool useMica, Version osVersion)
    {
        if (!IsMicaSupported(osVersion))
            return;

        var backdrop = useMica ? DwmsbtMainWindow : DwmsbtNone;
        if (TrySetWindowAttribute(hwnd, DwmwaSystemBackdropType, backdrop) && useMica)
        {
            window.Background = Brushes.Transparent;
        }
    }

    private static bool TrySetWindowAttribute(IntPtr hwnd, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(hwnd, attribute, ref value, Marshal.SizeOf<int>()) >= 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
