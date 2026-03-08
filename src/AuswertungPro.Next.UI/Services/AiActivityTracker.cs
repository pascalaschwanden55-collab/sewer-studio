using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Global thread-safe counter for active AI operations.
/// Usage: <c>using var _ = AiActivityTracker.Begin("label");</c>
/// </summary>
public static class AiActivityTracker
{
    private static int _activeCount;
    private static string _lastLabel = "";

    /// <summary>Fires on UI dispatcher when active state changes. Args: (isActive, label).</summary>
    public static event Action<bool, string>? ActiveChanged;

    /// <summary>True when at least one AI operation is running.</summary>
    public static bool IsActive => Volatile.Read(ref _activeCount) > 0;

    /// <summary>Label of the most recently started operation.</summary>
    public static string LastLabel => Volatile.Read(ref _lastLabel) ?? "";

    /// <summary>
    /// Begins tracking an AI operation. Dispose the returned token when complete.
    /// </summary>
    public static IDisposable Begin(string label)
    {
        Volatile.Write(ref _lastLabel, label);
        var prev = Interlocked.Increment(ref _activeCount);
        if (prev == 1)
            RaiseChanged(true, label);
        return new Token(label);
    }

    private static void End(string label)
    {
        var remaining = Interlocked.Decrement(ref _activeCount);
        if (remaining <= 0)
        {
            // Clamp to zero (safety)
            Interlocked.CompareExchange(ref _activeCount, 0, remaining);
            RaiseChanged(false, label);
        }
    }

    private static void RaiseChanged(bool isActive, string label)
    {
        var handler = ActiveChanged;
        if (handler is null) return;

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(() => handler(isActive, label), DispatcherPriority.Normal);
        else
            handler(isActive, label);
    }

    private sealed class Token : IDisposable
    {
        private readonly string _label;
        private int _disposed;

        public Token(string label) => _label = label;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                End(_label);
        }
    }
}
