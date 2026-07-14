using System.Diagnostics;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Common;

public sealed record ExplorerRevealLaunchPlan(
    bool Success,
    ProcessStartInfo? StartInfo,
    string? Error);

/// <summary>
/// Prueft das Explorer-Ziel und startet den Windows-Explorer.
/// </summary>
public sealed class ExplorerRevealLauncher : IExplorerRevealService
{
    private readonly Action<ProcessStartInfo> _startProcess;

    public ExplorerRevealLauncher(Action<ProcessStartInfo>? startProcess = null)
    {
        _startProcess = startProcess ?? (startInfo => Process.Start(startInfo));
    }

    public ExplorerRevealLaunchPlan BuildStartInfo(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return new ExplorerRevealLaunchPlan(false, null, "Pfad fehlt.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(targetPath);
        }
        catch (Exception ex)
        {
            return new ExplorerRevealLaunchPlan(false, null, ex.Message);
        }

        if (File.Exists(fullPath))
        {
            return new ExplorerRevealLaunchPlan(
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
            return new ExplorerRevealLaunchPlan(
                true,
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{fullPath}\"",
                    UseShellExecute = false
                },
                null);
        }

        return new ExplorerRevealLaunchPlan(
            false,
            null,
            "Datei oder Ordner nicht gefunden.");
    }

    public bool TryReveal(string? targetPath, out string? error)
    {
        var plan = BuildStartInfo(targetPath);
        if (!plan.Success || plan.StartInfo is null)
        {
            error = plan.Error;
            return false;
        }

        try
        {
            _startProcess(plan.StartInfo);
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
