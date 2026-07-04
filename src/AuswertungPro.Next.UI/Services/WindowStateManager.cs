using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Speichert und stellt Fensterposition/-groesse persistent wieder her.
/// Aufruf: <c>WindowStateManager.Track(this)</c> im Konstruktor nach InitializeComponent().
/// </summary>
public static class WindowStateManager
{
    private static AppSettings? _settings;

    public static void Configure(AppSettings settings)
        => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// Stellt gespeicherte Position/Groesse wieder her und registriert den Closing-Event.
    /// Schluessel = Typname (Altverhalten). Bewusst eigene Ueberladung statt optionalem
    /// Parameter, damit die Methodengruppe weiter als <c>Action&lt;Window&gt;</c> nutzbar bleibt
    /// (siehe PlayerWindowStateControls).
    /// </summary>
    public static void Track(Window window)
        => TrackWithKey(window, ResolveKey(window, null));

    /// <summary>
    /// Wie <see cref="Track(Window)"/>, aber mit explizitem Schluessel. Noetig, wenn mehrere
    /// logisch getrennte Fenster denselben Typ nutzen (z.B. generische <c>new Window</c> fuer
    /// abgekoppelte Panels) — sonst teilen sie sich einen Positions-Eintrag.
    /// </summary>
    public static void Track(Window window, string stateKey)
        => TrackWithKey(window, ResolveKey(window, stateKey));

    private static void TrackWithKey(Window window, string key)
    {
        RestoreBounds(window, key);
        window.Closing += (_, _) => SaveBounds(window, key);
    }

    /// <summary>Schluesselaufloesung: expliziter StateKey vor Typname (Altverhalten als Default).</summary>
    internal static string ResolveKey(Window window, string? stateKey)
        => string.IsNullOrWhiteSpace(stateKey) ? window.GetType().Name : stateKey!;

    private static void RestoreBounds(Window window, string key)
    {
        var settings = _settings;
        if (settings?.WindowStates is null)
            return;

        if (!settings.WindowStates.TryGetValue(key, out var bounds))
            return;

        if (bounds.Width < 100 || bounds.Height < 100)
            return;

        if (!IsVisibleOnAnyScreen(bounds.Left, bounds.Top, bounds.Width, bounds.Height))
            return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;

        if (bounds.IsMaximized)
            window.WindowState = WindowState.Maximized;
    }

    private static void SaveBounds(Window window, string key)
    {
        var settings = _settings;
        if (settings is null)
            return;

        settings.WindowStates ??= new();

        var rect = window.WindowState == WindowState.Maximized
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        if (rect.Width < 100 || rect.Height < 100)
            return;

        settings.WindowStates[key] = new WindowBounds
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = rect.Width,
            Height = rect.Height,
            IsMaximized = window.WindowState == WindowState.Maximized
        };

        settings.Save();
    }

    // --- Multi-Monitor visibility check via Win32 ---

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    private const uint MONITOR_DEFAULTTONULL = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private static bool IsVisibleOnAnyScreen(double left, double top, double width, double height)
    {
        var centerX = (int)(left + width / 2);
        var centerY = (int)(top + height / 2);
        var pt = new POINT { X = centerX, Y = centerY };

        // Returns IntPtr.Zero if point is not on any monitor
        var monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONULL);
        return monitor != IntPtr.Zero;
    }
}
