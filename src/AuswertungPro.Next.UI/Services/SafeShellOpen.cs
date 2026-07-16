using System;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.UI.Services;

public static class SafeShellOpen
{
    private static readonly ISafeShellOpenService Default = new SafeShellOpenService();

    internal static ISafeShellOpenService CompatibilityService
        => Default;

    [Obsolete("Globale Dienstwechsel sind nicht mehr erlaubt. ISafeShellOpenService direkt uebergeben.")]
    internal static void Use(ISafeShellOpenService service)
        => throw new NotSupportedException(
            "SafeShellOpen ist unveraenderlich. ISafeShellOpenService direkt uebergeben.");

    public static bool TryOpen(string? path, out string? error)
    {
        return CompatibilityService.TryOpen(path, out error);
    }
}
