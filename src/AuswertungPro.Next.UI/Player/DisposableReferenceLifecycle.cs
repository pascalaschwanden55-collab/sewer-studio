using System;

namespace AuswertungPro.Next.UI.Player;

public static class DisposableReferenceLifecycle
{
    public static T? DisposeAndClear<T>(T? current)
        where T : class, IDisposable
    {
        current?.Dispose();
        return null;
    }
}
