using System.Threading;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.DataPage;

namespace AuswertungPro.Next.UI.DataPage;

public static class SchachtFileTargetResolver
{
    private static ISchachtFileTargetResolver _current = new SchachtFileTargetPathResolver();

    internal static ISchachtFileTargetResolver CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(ISchachtFileTargetResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _current, resolver);
    }

    public static string? ResolvePdfPath(SchachtRecord record, string? projectFilePath)
    {
        return CompatibilityService.ResolvePdfPath(record, projectFilePath);
    }

    public static string? ResolveExplorerTarget(SchachtRecord record, string? projectFilePath)
    {
        return CompatibilityService.ResolveExplorerTarget(record, projectFilePath);
    }
}
