using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Opt-in Live-Tests gegen einen lokal laufenden Python-Sidecar auf
/// http://localhost:8100. Default-Lauf ueberspringt diese Tests via
/// <c>.runsettings</c>-Filter <c>Category!=LiveSidecar</c>. Ausfuehrung
/// per <c>.runsettings.live</c> oder <c>--filter "Category=LiveSidecar"</c>
/// in Kombination mit leerem RunSettings-Filter.
///
/// Tests pruefen nur den HTTP-Vertrag, keine Modell-Heavy-Calls
/// (kein YOLO, kein SAM, keine Frames). Wenn der Sidecar nicht erreichbar
/// ist oder Voraussetzungen fehlen, gibt der Test mit Output-Log zurueck
/// statt zu failen — xUnit 2.7 hat kein natives Runtime-Skip und der User
/// wollte keine neue Test-Dependency.
/// </summary>
public class SidecarLiveContractTests
{
    private const string SidecarBaseUrl = "http://localhost:8100";
    private static readonly TimeSpan ReachabilityTimeout = TimeSpan.FromSeconds(2);

    private readonly ITestOutputHelper _output;

    public SidecarLiveContractTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "LiveSidecar")]
    public async Task LiveHealth_AgainstRealSidecar_ReturnsOkWithStatus()
    {
        if (!await SidecarReachableAsync())
        {
            _output.WriteLine($"SKIP: Sidecar nicht erreichbar auf {SidecarBaseUrl}/health");
            return;
        }

        // /health ist im Sidecar als public registriert — kein Token noetig.
        var client = new VisionPipelineClient(new Uri(SidecarBaseUrl), new HttpClient(), authToken: null);
        var result = await client.HealthCheckAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.Status),
            "Sidecar /health hat kein status-Feld geliefert");
        _output.WriteLine($"Live-Sidecar OK: status={result.Status} version={result.Version}");
    }

    [Fact]
    [Trait("Category", "LiveSidecar")]
    public async Task LiveAuth_PostWithoutToken_Returns401_WhenAuthEnabled()
    {
        if (!await SidecarReachableAsync())
        {
            _output.WriteLine($"SKIP: Sidecar nicht erreichbar auf {SidecarBaseUrl}");
            return;
        }

        if (IsAuthDisabled())
        {
            _output.WriteLine("SKIP: SEWER_SIDECAR_AUTH=disabled — kein 401 erwartbar");
            return;
        }

        // Geschuetzter POST-Endpoint ohne Token: erwartet 401 Unauthorized.
        // Body absichtlich minimal — wir testen den Auth-Vertrag, nicht YOLO.
        using var http = new HttpClient { Timeout = ReachabilityTimeout };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{SidecarBaseUrl}/detect/yolo")
        {
            Content = new StringContent(
                """{"image_base64":"","confidence_threshold":0.3}""",
                Encoding.UTF8, "application/json")
        };

        using var resp = await http.SendAsync(req);
        _output.WriteLine($"POST /detect/yolo ohne Token -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    [Trait("Category", "LiveSidecar")]
    public async Task LiveUnknownEndpoint_Returns404()
    {
        if (!await SidecarReachableAsync())
        {
            _output.WriteLine($"SKIP: Sidecar nicht erreichbar auf {SidecarBaseUrl}");
            return;
        }

        // 404-Test mit Token senden, sonst kann Auth-Middleware vorher
        // korrekt 401 liefern und der Test wuerde fehlschlagen.
        var token = TryReadToken();
        if (token is null && !IsAuthDisabled())
        {
            _output.WriteLine("SKIP: kein Token gefunden + Auth aktiv — 404 nicht von 401 unterscheidbar");
            return;
        }

        using var http = new HttpClient { Timeout = ReachabilityTimeout };
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{SidecarBaseUrl}/this/endpoint/does/not/exist");
        if (token is not null)
            req.Headers.TryAddWithoutValidation("X-Sidecar-Token", token);

        using var resp = await http.SendAsync(req);
        _output.WriteLine($"GET /this/endpoint/does/not/exist -> {(int)resp.StatusCode}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<bool> SidecarReachableAsync()
    {
        using var http = new HttpClient { Timeout = ReachabilityTimeout };
        try
        {
            using var resp = await http.GetAsync($"{SidecarBaseUrl}/health");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAuthDisabled()
    {
        var auth = Environment.GetEnvironmentVariable("SEWER_SIDECAR_AUTH");
        return string.Equals(auth, "disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadToken()
    {
        // 1. Env-Variable hat Vorrang
        var fromEnv = Environment.GetEnvironmentVariable("SEWER_SIDECAR_TOKEN");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        // 2. Token-Datei %LOCALAPPDATA%/SewerStudio/.sidecar_token
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
                return null;

            var path = Path.Combine(local, "SewerStudio", ".sidecar_token");
            if (File.Exists(path))
            {
                var raw = File.ReadAllText(path).Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    return raw;
            }
        }
        catch
        {
            // best-effort: Token-Datei optional, kein hartes Failure
        }

        return null;
    }
}
