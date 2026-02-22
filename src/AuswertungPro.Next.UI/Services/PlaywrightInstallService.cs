using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI;

public sealed class PlaywrightInstallService : IPlaywrightInstallService
{
    private readonly ILogger _logger;

    public PlaywrightInstallService(ILogger logger)
    {
        _logger = logger;
    }

    public bool IsChromiumInstalled()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(local, "ms-playwright");
            if (!Directory.Exists(dir))
                return false;

            // Heuristik: in diesem Ordner liegen Unterordner wie "chromium-<rev>".
            var anyChromium = Directory.EnumerateDirectories(dir, "chromium-*", SearchOption.TopDirectoryOnly).Any();
            return anyChromium;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PlaywrightInstallResult> InstallChromiumAsync(CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(baseDir, "playwright.ps1");
        if (!File.Exists(scriptPath))
        {
            return new PlaywrightInstallResult(
                Success: false,
                ExitCode: -1,
                Tool: "pwsh",
                ScriptPath: scriptPath,
                StdOut: "",
                StdErr: "playwright.ps1 nicht gefunden. Bitte zuerst bauen/restore ausführen (oder Microsoft.Playwright im UI-Projekt referenzieren)."
            );
        }

        // bevorzugt PowerShell 7 (pwsh), fallback auf Windows PowerShell.
        try
        {
            return await RunInstallAsync("pwsh", scriptPath, ct);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning(ex, "pwsh not found, falling back to powershell.exe");
            return await RunInstallAsync("powershell", scriptPath, ct);
        }
    }

    private async Task<PlaywrightInstallResult> RunInstallAsync(string tool, string scriptPath, CancellationToken ct)
    {
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" install chromium";

        var psi = new ProcessStartInfo
        {
            FileName = tool,
            Arguments = args,
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _logger.LogInformation("Installing Playwright Chromium via {Tool}: {Args}", tool, args);

        try
        {
            p.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {Tool}", tool);
            return new PlaywrightInstallResult(false, -1, tool, scriptPath, "", ex.Message);
        }

        var stdOutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = p.StandardError.ReadToEndAsync(ct);

        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        var ok = p.ExitCode == 0;
        if (ok)
            _logger.LogInformation("Playwright Chromium installed successfully");
        else
            _logger.LogWarning("Playwright install chromium failed with exit code {ExitCode}", p.ExitCode);

        return new PlaywrightInstallResult(ok, p.ExitCode, tool, scriptPath, stdOut, stdErr);
    }
}
