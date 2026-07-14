using System.Diagnostics;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.UI.Services;

public sealed record ExplorerRevealStartPlan(bool Success, ProcessStartInfo? StartInfo, string? Error);

public static class ExplorerRevealService
{
    private static readonly ExplorerRevealLauncher DefaultLauncher = new();

    internal static IExplorerRevealService DefaultService => DefaultLauncher;

    public static ExplorerRevealStartPlan BuildStartInfo(string? targetPath)
    {
        var plan = DefaultLauncher.BuildStartInfo(targetPath);
        return new ExplorerRevealStartPlan(
            plan.Success,
            plan.StartInfo,
            plan.Error);
    }

    public static bool TryReveal(string? targetPath, out string? error)
        => DefaultLauncher.TryReveal(targetPath, out error);
}
