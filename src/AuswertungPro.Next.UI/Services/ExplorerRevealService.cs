using System.Diagnostics;
using System.IO;

namespace AuswertungPro.Next.UI.Services;

public sealed record ExplorerRevealStartPlan(bool Success, ProcessStartInfo? StartInfo, string? Error);

public static class ExplorerRevealService
{
    public static ExplorerRevealStartPlan BuildStartInfo(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return new ExplorerRevealStartPlan(false, null, "Pfad fehlt.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(targetPath);
        }
        catch (Exception ex)
        {
            return new ExplorerRevealStartPlan(false, null, ex.Message);
        }

        if (File.Exists(fullPath))
        {
            return new ExplorerRevealStartPlan(
                true,
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{fullPath}\"",
                    UseShellExecute = false
                },
                null);
        }

        if (Directory.Exists(fullPath))
        {
            return new ExplorerRevealStartPlan(
                true,
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{fullPath}\"",
                    UseShellExecute = false
                },
                null);
        }

        return new ExplorerRevealStartPlan(false, null, "Datei oder Ordner nicht gefunden.");
    }

    public static bool TryReveal(string? targetPath, out string? error)
    {
        var plan = BuildStartInfo(targetPath);
        if (!plan.Success || plan.StartInfo is null)
        {
            error = plan.Error;
            return false;
        }

        try
        {
            Process.Start(plan.StartInfo);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
