using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Scriban;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

public sealed class OfferHtmlToPdfRenderer
{
    public async Task RenderAsync(
        OfferPdfModel model,
        string templatePath,
        string outputPdfPath,
        string? logoPngPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            throw new FileNotFoundException("Offer template not found", templatePath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath) ?? ".");

        var templateText = await File.ReadAllTextAsync(templatePath, ct);
        var template = Template.Parse(templateText);
        if (template.HasErrors)
        {
            var msg = string.Join("; ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException("Template errors: " + msg);
        }

        model.LogoDataUri = string.IsNullOrWhiteSpace(logoPngPath) || !File.Exists(logoPngPath)
            ? BuildDefaultLogoDataUri()
            : LoadPngAsDataUri(logoPngPath);

        var html = template.Render(model, memberRenamer: m => m.Name);

        try
        {
            await RenderPdfWithPlaywrightAsync(html, outputPdfPath, ct);
        }
        catch (PlaywrightException ex) when (IsMissingBrowserException(ex))
        {
            var install = await TryInstallChromiumAsync(ct);
            if (install.Success)
            {
                try
                {
                    await RenderPdfWithPlaywrightAsync(html, outputPdfPath, ct);
                    return;
                }
                catch (PlaywrightException retryEx)
                {
                    throw new InvalidOperationException(
                        BuildPlaywrightHint(retryEx, outputPdfPath, install.Details), retryEx);
                }
            }

            throw new InvalidOperationException(
                BuildPlaywrightHint(ex, outputPdfPath, install.Details), ex);
        }
        catch (PlaywrightException ex)
        {
            throw new InvalidOperationException(BuildPlaywrightHint(ex, outputPdfPath), ex);
        }
    }

    private static async Task RenderPdfWithPlaywrightAsync(
        string html,
        string outputPdfPath,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        try
        {
            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
            });

            try
            {
                await page.SetContentAsync(html, new PageSetContentOptions
                {
                    WaitUntil = WaitUntilState.Load
                });

                var footer = @"
<div style='width:100%; font-family:Arial; font-size:9px; color:#666; padding:0 12mm;'>
  <div style='display:flex; justify-content:space-between;'>
    <span>Abwasser Uri | Zentrale Dienste | Giessenstrasse 46 | 6460 Altdorf | info@abwasser-uri.ch | T 041 875 00 90</span>
    <span>Seite <span class='pageNumber'></span> / <span class='totalPages'></span></span>
  </div>
</div>";

                await page.PdfAsync(new PagePdfOptions
                {
                    Path = outputPdfPath,
                    Format = "A4",
                    PrintBackground = true,
                    DisplayHeaderFooter = true,
                    HeaderTemplate = "<div></div>",
                    FooterTemplate = footer,
                    Margin = new Margin
                    {
                        Top = "16mm",
                        Bottom = "18mm",
                        Left = "14mm",
                        Right = "14mm"
                    }
                });
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private static string LoadPngAsDataUri(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var b64 = Convert.ToBase64String(bytes);
        return $"data:image/png;base64,{b64}";
    }

    private static string BuildDefaultLogoDataUri()
    {
        const string svg = """
<svg xmlns='http://www.w3.org/2000/svg' width='680' height='160' viewBox='0 0 680 160'>
  <rect width='680' height='160' fill='white'/>
  <path d='M55 18 C42 43 20 76 10 112 L10 118 L100 118 L100 112 C90 76 68 43 55 18 Z' fill='#b8b2a8'/>
  <path d='M5 118 A50 50 0 0 0 105 118 Z' fill='#005c84'/>
  <path d='M42 81 C28 94 27 111 43 121 C55 129 73 122 74 110 C61 117 49 112 49 101 C49 95 52 89 57 83 C54 82 49 81 42 81 Z' fill='white'/>
  <text x='140' y='92' font-family='Segoe UI, Arial, sans-serif' font-size='60' font-weight='700' fill='#b8b2a8'>ABWASSER</text>
  <text x='140' y='148' font-family='Segoe UI, Arial, sans-serif' font-size='88' font-weight='800' fill='#005c84'>URI</text>
</svg>
""";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return "data:image/svg+xml;base64," + b64;
    }

    private static bool IsMissingBrowserException(PlaywrightException ex)
    {
        var msg = ex.Message ?? "";
        return msg.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("please run the following command to download new browsers", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("playwright install", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("headless_shell", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<InstallAttemptResult> TryInstallChromiumAsync(CancellationToken ct)
    {
        var baseDir = AppContext.BaseDirectory;
        var ps1 = Path.Combine(baseDir, "playwright.ps1");
        if (!File.Exists(ps1))
            return new InstallAttemptResult(false, "playwright.ps1 wurde im Ausgabeverzeichnis nicht gefunden.");

        var pwshResult = await RunPlaywrightInstallAsync("pwsh", ps1, baseDir, ct);
        if (pwshResult is not null)
            return pwshResult;

        var powershellResult = await RunPlaywrightInstallAsync("powershell", ps1, baseDir, ct);
        if (powershellResult is not null)
            return powershellResult;

        return new InstallAttemptResult(false, "Weder 'pwsh' noch 'powershell' konnte gestartet werden.");
    }

    private static async Task<InstallAttemptResult?> RunPlaywrightInstallAsync(
        string shellExe,
        string scriptPath,
        string workingDir,
        CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = shellExe,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("install");
            psi.ArgumentList.Add("chromium");

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(ct);

            var stdOut = (await stdOutTask).Trim();
            var stdErr = (await stdErrTask).Trim();

            var details = $"Installer: {shellExe}, ExitCode={process.ExitCode}";
            if (!string.IsNullOrWhiteSpace(stdErr))
                details += Environment.NewLine + "stderr: " + Shrink(stdErr);
            if (!string.IsNullOrWhiteSpace(stdOut))
                details += Environment.NewLine + "stdout: " + Shrink(stdOut);

            return new InstallAttemptResult(process.ExitCode == 0, details);
        }
        catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            return new InstallAttemptResult(false, $"Installer konnte nicht gestartet werden ({shellExe}): {ex.Message}");
        }
    }

    private static string Shrink(string value)
    {
        const int maxLen = 1200;
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLen)
            return value;
        return value.Substring(0, maxLen) + "...";
    }

    private static string BuildPlaywrightHint(Exception ex, string outputPdfPath, string? installDetails = null)
    {
        var baseDir = AppContext.BaseDirectory;
        var ps1 = Path.Combine(baseDir, "playwright.ps1");
        var cmd = File.Exists(ps1)
            ? $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{ps1}\" install chromium"
            : "powershell -NoProfile -ExecutionPolicy Bypass -File <dein-output-ordner>/playwright.ps1 install chromium";

        var hint =
            "PDF-Export konnte nicht gestartet werden (Playwright/Chromium fehlt oder ist nicht ausfuehrbar)." + Environment.NewLine +
            "Details: " + ex.Message + Environment.NewLine + Environment.NewLine +
            "Fix: einmalig Browser installieren:" + Environment.NewLine +
            cmd + Environment.NewLine + Environment.NewLine +
            "Output: " + outputPdfPath;

        if (!string.IsNullOrWhiteSpace(installDetails))
            hint += Environment.NewLine + Environment.NewLine + "Auto-Install Versuch:" + Environment.NewLine + installDetails;

        return hint;
    }

    private sealed record InstallAttemptResult(bool Success, string Details);
}
