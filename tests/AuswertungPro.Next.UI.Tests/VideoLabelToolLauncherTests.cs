using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.Services;

public sealed class VideoLabelToolLauncherTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "sewer-video-label-tool-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateLaunchPlan_findet_tool_aus_unterordner_und_baut_server_befehl()
    {
        var repoRoot = CreateRepoWithVideoLabelTool();
        var nestedAppDirectory = Path.Combine(repoRoot, "src", "AuswertungPro.Next.UI", "bin", "Debug", "net10.0-windows");
        Directory.CreateDirectory(nestedAppDirectory);
        var starter = new CapturingProcessStarter();
        var launcher = new VideoLabelToolLauncher(starter, () => nestedAppDirectory, () => null);

        var plan = launcher.CreateLaunchPlan(new VideoLabelToolLaunchOptions(Port: 8210));

        Assert.Equal(Path.Combine(repoRoot, "tools", "VideoLabelTool"), plan.ToolDirectory);
        Assert.Equal(Path.Combine(repoRoot, "tools", "VideoLabelTool", "server.py"), plan.ServerScriptPath);
        Assert.Equal(new Uri("http://localhost:8210/"), plan.Url);
        Assert.Equal("python", plan.ServerStartInfo.FileName);
        Assert.Equal(plan.ToolDirectory, plan.ServerStartInfo.WorkingDirectory);
        Assert.Contains(plan.ServerScriptPath, plan.ServerStartInfo.ArgumentList);
        Assert.Contains("--port", plan.ServerStartInfo.ArgumentList);
        Assert.Contains("8210", plan.ServerStartInfo.ArgumentList);
        Assert.NotNull(plan.BrowserStartInfo);
        Assert.True(plan.BrowserStartInfo!.UseShellExecute);
        Assert.Equal("http://localhost:8210/", plan.BrowserStartInfo.FileName);
    }

    [Fact]
    public void Launch_startet_server_nur_einmal_aber_oeffnet_browser_jedes_mal()
    {
        var repoRoot = CreateRepoWithVideoLabelTool();
        var starter = new CapturingProcessStarter();
        var launcher = new VideoLabelToolLauncher(starter, () => repoRoot, () => null);

        var first = launcher.Launch(new VideoLabelToolLaunchOptions(Port: 8200));
        var second = launcher.Launch(new VideoLabelToolLaunchOptions(Port: 8200));

        Assert.True(first.ServerStarted);
        Assert.False(second.ServerStarted);
        Assert.Equal(1, starter.Started.Count(x => string.Equals(x.FileName, "python", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, starter.Started.Count(x => x.UseShellExecute && x.FileName.StartsWith("http://localhost:8200/", StringComparison.Ordinal)));
    }

    [Fact]
    public void CreateLaunchPlan_nutzt_explizites_tool_verzeichnis()
    {
        var repoRoot = CreateRepoWithVideoLabelTool();
        var explicitToolDirectory = Path.Combine(repoRoot, "tools", "VideoLabelTool");
        var otherDirectory = Path.Combine(_tempRoot, "unrelated", "bin");
        Directory.CreateDirectory(otherDirectory);
        var launcher = new VideoLabelToolLauncher(new CapturingProcessStarter(), () => otherDirectory, () => explicitToolDirectory);

        var plan = launcher.CreateLaunchPlan(new VideoLabelToolLaunchOptions(Port: 8220, PriorityPath: @"C:\tmp\priority.json", Limit: 25));

        Assert.Equal(explicitToolDirectory, plan.ToolDirectory);
        Assert.Contains("--priority", plan.ServerStartInfo.ArgumentList);
        Assert.Contains(@"C:\tmp\priority.json", plan.ServerStartInfo.ArgumentList);
        Assert.Contains("--limit", plan.ServerStartInfo.ArgumentList);
        Assert.Contains("25", plan.ServerStartInfo.ArgumentList);
    }

    [Fact]
    public void CreateLaunchPlan_meldet_fehler_wenn_tool_fehlt()
    {
        var emptyRoot = Path.Combine(_tempRoot, "empty");
        Directory.CreateDirectory(emptyRoot);
        var launcher = new VideoLabelToolLauncher(new CapturingProcessStarter(), () => emptyRoot, () => null);

        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            launcher.CreateLaunchPlan(new VideoLabelToolLaunchOptions()));

        Assert.Contains("VideoLabelTool", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private string CreateRepoWithVideoLabelTool()
    {
        var repoRoot = Path.Combine(_tempRoot, "repo");
        var toolDir = Path.Combine(repoRoot, "tools", "VideoLabelTool");
        Directory.CreateDirectory(toolDir);
        File.WriteAllText(Path.Combine(toolDir, "server.py"), "# test server");
        return repoRoot;
    }

    private sealed class CapturingProcessStarter : IVideoLabelToolProcessStarter
    {
        public List<ProcessStartInfo> Started { get; } = new();

        public void Start(ProcessStartInfo startInfo)
            => Started.Add(startInfo);
    }
}
