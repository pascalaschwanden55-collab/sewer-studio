using System;
using System.Threading;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.UI.Mapping;

public static class KatasterXtfPathResolver
{
    private static IKatasterXtfPathResolver _current = new KatasterXtfFilePathResolver();

    internal static IKatasterXtfPathResolver CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(IKatasterXtfPathResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _current, resolver);
    }

    public static string Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return CompatibilityService.Resolve(
            settings.AbwasserkatasterXtfPath,
            settings.KantonUriXtfDirectory);
    }

    public static string Resolve(string? explicitPath, string? directoryPath)
    {
        return CompatibilityService.Resolve(explicitPath, directoryPath);
    }

    public static string? TryFindKatasterXtf(string? directoryPath)
    {
        return CompatibilityService.TryFindKatasterXtf(directoryPath);
    }
}
