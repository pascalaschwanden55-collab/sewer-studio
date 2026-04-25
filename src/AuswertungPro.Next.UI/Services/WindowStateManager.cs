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
    /// <summary>
    /// Stellt gespeicherte Position/Groesse wieder her und registriert
    /// den Closing-Event zum automatischen Speichern.
    /// </summary>
    public static void Track(Window window)
    {
        var key = window.GetType().Name;
        // Restore in try/catch — fehlerhafte Settings duerfen das Fenster
        // nicht am Oeffnen hindern.
        try { RestoreBounds(window, key); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowStateManager] RestoreBounds {key}: {ex.Message}");
        }

        // KRITISCH (Audit R-C1, 2026-04-25): SaveBounds ruft settings.Save(),
        // das bei IO-Fehler (Disk voll, AV-Lock, Profile-Roaming-Snapshot)
        // werfen kann. Eine Exception aus diesem Closing-Hook eskaliert ueber
        // DispatcherUnhandledException und kann je nach Race mit MainWindow-
        // Ownership die ganze App killen — genau das vom User berichtete
        // "Fenster zu -> App weg"-Symptom. 28 Fenster nutzen diesen Track-
        // Aufruf, ein zentraler try/catch schuetzt sie alle.
        window.Closing += (_, _) =>
        {
            try { SaveBounds(window, key); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowStateManager] SaveBounds {key}: {ex.Message}");
            }
        };
    }

    private static AppSettings? GetSettings()
    {
        try { return (App.Services as ServiceProvider)?.Settings; }
        catch { return null; }
    }

    private static void RestoreBounds(Window window, string key)
    {
        var settings = GetSettings();
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
        var settings = GetSettings();
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
