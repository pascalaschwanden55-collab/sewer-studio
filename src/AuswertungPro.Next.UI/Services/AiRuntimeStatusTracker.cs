using System;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Services;

public sealed record AiRuntimeStatus(
    bool IsVisible,
    string Title,
    string StatusText,
    string ModelText);

public static class AiRuntimeStatusTracker
{
    private static readonly object Gate = new();
    private static AiRuntimeStatus _current = new(false, "", "", "");

    public static event Action<AiRuntimeStatus>? Changed;

    public static AiRuntimeStatus Current
    {
        get
        {
            lock (Gate)
                return _current;
        }
    }

    public static void MarkStarting(string? modelText)
        => Set(new AiRuntimeStatus(true, "KI STARTET", "KI startet...", Normalize(modelText)));

    public static void MarkReady(string? modelText, bool hasWarnings, string? statusText = null)
        => Set(new AiRuntimeStatus(
            true,
            hasWarnings ? "KI WARNUNG" : "KI BEREIT",
            Normalize(statusText) is { Length: > 0 } text
                ? text
                : hasWarnings ? "KI gestartet mit Warnung" : "KI bereit",
            Normalize(modelText)));

    public static void ResetForTests()
        => Set(new AiRuntimeStatus(false, "", "", ""));

    private static void Set(AiRuntimeStatus status)
    {
        lock (Gate)
            _current = status;

        RaiseChanged(status);
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

    private static void RaiseChanged(AiRuntimeStatus status)
    {
        var handler = Changed;
        if (handler is null)
            return;

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(() => handler(status), DispatcherPriority.Normal);
        else
            handler(status);
    }
}
