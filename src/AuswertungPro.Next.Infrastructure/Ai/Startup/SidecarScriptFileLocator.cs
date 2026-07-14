using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Sucht das Sidecar-Startskript aufwärts und löst die Windows-PowerShell auf.
/// </summary>
public sealed class SidecarScriptFileLocator : ISidecarScriptLocator
{
    private readonly Func<IEnumerable<string>> _startDirectoryProvider;
    private readonly Func<string?> _windowsDirectoryProvider;

    public SidecarScriptFileLocator()
    {
        _startDirectoryProvider = static () =>
            [AppContext.BaseDirectory, Environment.CurrentDirectory];
        _windowsDirectoryProvider = static () =>
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    }

    public SidecarScriptFileLocator(
        IEnumerable<string> startDirectories,
        string? windowsDirectory)
    {
        ArgumentNullException.ThrowIfNull(startDirectories);
        var startDirectorySnapshot = startDirectories.ToArray();
        _startDirectoryProvider = () => startDirectorySnapshot;
        _windowsDirectoryProvider = () => windowsDirectory;
    }

    public string? FindDefaultSidecarScript()
    {
        foreach (var start in _startDirectoryProvider()
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "sidecar",
                    "start_sidecar.ps1");
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }
        }

        return null;
    }

    public string ResolvePowerShellExe()
    {
        var windowsDirectory = _windowsDirectoryProvider();
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            var candidate = Path.Combine(
                windowsDirectory,
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return "powershell.exe";
    }
}
