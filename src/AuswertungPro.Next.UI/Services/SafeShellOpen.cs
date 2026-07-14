using System;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.UI.Services;

public static class SafeShellOpen
{
    private static ISafeShellOpenService _current = new SafeShellOpenService();

    internal static ISafeShellOpenService CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(ISafeShellOpenService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        Volatile.Write(ref _current, service);
    }

    public static bool TryOpen(string? path, out string? error)
    {
        return CompatibilityService.TryOpen(path, out error);
    }
}
