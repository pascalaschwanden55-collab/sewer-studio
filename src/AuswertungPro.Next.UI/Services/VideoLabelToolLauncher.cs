using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AuswertungPro.Next.UI.Services;

public sealed record VideoLabelToolLaunchOptions(
    int Port = 8200,
    string? PriorityPath = null,
    int? Limit = null,
    string? Classes = null,
    bool OpenBrowser = true);

public sealed record VideoLabelToolLaunchPlan(
    string ToolDirectory,
    string ServerScriptPath,
    Uri Url,
    ProcessStartInfo ServerStartInfo,
    ProcessStartInfo? BrowserStartInfo);

public sealed record VideoLabelToolLaunchResult(Uri Url, bool ServerStarted);

public interface IVideoLabelToolProcessStarter
{
    void Start(ProcessStartInfo startInfo);
}

public sealed class DefaultVideoLabelToolProcessStarter : IVideoLabelToolProcessStarter
{
    public void Start(ProcessStartInfo startInfo)
        => Process.Start(startInfo);
}

public sealed class VideoLabelToolLauncher
{
    private const string ToolRelativePath = @"tools\VideoLabelTool";
    private const string ServerFileName = "server.py";

    private readonly IVideoLabelToolProcessStarter _processStarter;
    private readonly Func<string> _currentDirectoryProvider;
    private readonly Func<string?> _explicitToolDirectoryProvider;
    private bool _serverStarted;

    public VideoLabelToolLauncher(
        IVideoLabelToolProcessStarter? processStarter = null,
        Func<string>? currentDirectoryProvider = null,
        Func<string?>? explicitToolDirectoryProvider = null)
    {
        _processStarter = processStarter ?? new DefaultVideoLabelToolProcessStarter();
        _currentDirectoryProvider = currentDirectoryProvider ?? (() => AppContext.BaseDirectory);
        _explicitToolDirectoryProvider = explicitToolDirectoryProvider
            ?? (() => Environment.GetEnvironmentVariable("SEWER_VIDEO_LABEL_TOOL_DIR"));
    }

    public VideoLabelToolLaunchPlan CreateLaunchPlan(VideoLabelToolLaunchOptions? options = null)
    {
        options ??= new VideoLabelToolLaunchOptions();
        var toolDirectory = ResolveToolDirectory();
        var serverScriptPath = Path.Combine(toolDirectory, ServerFileName);
        var url = new Uri($"http://localhost:{options.Port}/");

        var serverStartInfo = new ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = toolDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        serverStartInfo.ArgumentList.Add(serverScriptPath);
        serverStartInfo.ArgumentList.Add("--port");
        serverStartInfo.ArgumentList.Add(options.Port.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(options.PriorityPath))
        {
            serverStartInfo.ArgumentList.Add("--priority");
            serverStartInfo.ArgumentList.Add(options.PriorityPath);
        }

        if (options.Limit is > 0)
        {
            serverStartInfo.ArgumentList.Add("--limit");
            serverStartInfo.ArgumentList.Add(options.Limit.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(options.Classes))
        {
            serverStartInfo.ArgumentList.Add("--classes");
            serverStartInfo.ArgumentList.Add(options.Classes);
        }

        var browserStartInfo = options.OpenBrowser
            ? new ProcessStartInfo(url.ToString()) { UseShellExecute = true }
            : null;

        return new VideoLabelToolLaunchPlan(
            toolDirectory,
            serverScriptPath,
            url,
            serverStartInfo,
            browserStartInfo);
    }

    public VideoLabelToolLaunchResult Launch(VideoLabelToolLaunchOptions? options = null)
    {
        var plan = CreateLaunchPlan(options);
        var serverStarted = false;

        if (!_serverStarted)
        {
            _processStarter.Start(plan.ServerStartInfo);
            _serverStarted = true;
            serverStarted = true;
        }

        if (plan.BrowserStartInfo is not null)
            _processStarter.Start(plan.BrowserStartInfo);

        return new VideoLabelToolLaunchResult(plan.Url, serverStarted);
    }

    private string ResolveToolDirectory()
    {
        var explicitToolDirectory = _explicitToolDirectoryProvider()?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitToolDirectory))
        {
            var fullExplicit = Path.GetFullPath(explicitToolDirectory);
            if (IsToolDirectory(fullExplicit))
                return fullExplicit;

            throw new DirectoryNotFoundException(
                $"VideoLabelTool nicht gefunden: {fullExplicit}");
        }

        var searchRoots = new[] { _currentDirectoryProvider() }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in searchRoots)
        {
            var found = FindToolDirectoryFrom(root);
            if (found is not null)
                return found;
        }

        throw new DirectoryNotFoundException(
            "VideoLabelTool nicht gefunden. Erwartet wird tools\\VideoLabelTool\\server.py im Repo oder SEWER_VIDEO_LABEL_TOOL_DIR.");
    }

    private static string? FindToolDirectoryFrom(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (IsToolDirectory(current.FullName))
                return current.FullName;

            var candidate = Path.Combine(current.FullName, ToolRelativePath);
            if (IsToolDirectory(candidate))
                return candidate;

            current = current.Parent;
        }

        return null;
    }

    private static bool IsToolDirectory(string directory)
        => File.Exists(Path.Combine(directory, ServerFileName));
}
