using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.DataPage;

namespace AuswertungPro.Next.UI.DataPage;

public static class SchachtFileTargetResolver
{
    private static readonly ISchachtFileTargetResolver Default = new SchachtFileTargetPathResolver();

    internal static ISchachtFileTargetResolver CompatibilityService
        => Default;

    public static string? ResolvePdfPath(SchachtRecord record, string? projectFilePath)
    {
        return CompatibilityService.ResolvePdfPath(record, projectFilePath);
    }

    public static string? ResolveExplorerTarget(SchachtRecord record, string? projectFilePath)
    {
        return CompatibilityService.ResolveExplorerTarget(record, projectFilePath);
    }
}
