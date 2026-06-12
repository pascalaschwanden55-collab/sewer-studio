using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public static class LiveControlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<object> HealthAsync(string liveControlUrl)
        => await SendAsync(liveControlUrl, HttpMethod.Get, "health", null).ConfigureAwait(false);

    public static async Task<object> SetResourceBrushAsync(string liveControlUrl, string key, string color)
        => await SendAsync(
            liveControlUrl,
            HttpMethod.Post,
            "resource/brush",
            new { key, color }).ConfigureAwait(false);

    public static async Task<object> SetButtonBackgroundAsync(
        string liveControlUrl,
        string? target,
        string color,
        int? maxMatches)
        => await SendAsync(
            liveControlUrl,
            HttpMethod.Post,
            "buttons/background",
            new { target, color, max_matches = maxMatches }).ConfigureAwait(false);

    public static async Task<object> RetryHoldingAsync(string liveControlUrl, string haltungsname)
        => await SendAsync(
            liveControlUrl,
            HttpMethod.Post,
            "pipeline/retry",
            new { haltungsname }).ConfigureAwait(false);

    private static async Task<object> SendAsync(string liveControlUrl, HttpMethod method, string path, object? body)
    {
        if (!TryBuildLoopbackUrl(liveControlUrl, path, out var url, out var urlError))
        {
            return new
            {
                ok = false,
                live_control_url = liveControlUrl,
                error = urlError
            };
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var request = new HttpRequestMessage(method, url!);

            // Token: bevorzugt aus der Env-Var, sonst aus der vom App-Server erzeugten Token-Datei.
            var token = ResolveToken();
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.TryAddWithoutValidation("X-Live-Control-Token", token);

            if (body is not null)
                request.Content = JsonContent.Create(body, options: JsonOptions);

            using var response = await http.SendAsync(request).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);

            return new
            {
                ok = response.IsSuccessStatusCode,
                live_control_url = url!.ToString(),
                status_code = (int)response.StatusCode,
                response = doc.RootElement.Clone()
            };
        }
        catch (Exception ex)
        {
            return new
            {
                ok = false,
                live_control_url = url!.ToString(),
                error = ex.Message
            };
        }
    }

    /// <summary>
    /// Live-Control-Token ermitteln: zuerst Env-Var, sonst die vom App-Server angelegte Token-Datei
    /// (gleicher Pfad wie AppSettings.AppDataDir + ".live_control_token").
    /// </summary>
    private static string? ResolveToken()
    {
        var envToken = Environment.GetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;

        try
        {
            var overridePath = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
            var dir = string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SewerStudio")
                : overridePath;
            var path = Path.Combine(dir, ".live_control_token");
            if (File.Exists(path))
                return File.ReadAllText(path).Trim();
        }
        catch
        {
            // Token-Datei nicht lesbar - ohne Token senden (Server antwortet dann 401).
        }

        return null;
    }

    public static bool TryBuildLoopbackUrl(string? liveControlUrl, string path, out Uri? url, out string? error)
    {
        url = null;
        error = null;

        var baseUrl = string.IsNullOrWhiteSpace(liveControlUrl)
            ? "http://127.0.0.1:8765/"
            : liveControlUrl;
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            error = "live_control_url ist keine gueltige absolute URL.";
            return false;
        }

        if (baseUri.Scheme is not ("http" or "https"))
        {
            error = "live_control_url muss http/https verwenden und auf loopback zeigen.";
            return false;
        }

        if (!IPAddressLoopbackPolicy.IsLoopbackHost(baseUri.Host))
        {
            error = "live_control_url muss auf loopback zeigen (localhost, 127.0.0.1 oder ::1).";
            return false;
        }

        var relativePath = path.TrimStart('/');
        var candidate = new Uri(baseUri, relativePath);
        if (!IPAddressLoopbackPolicy.IsLoopbackHost(candidate.Host))
        {
            error = "live_control_url darf nicht auf einen externen Host umleiten.";
            return false;
        }

        url = candidate;
        return true;
    }
}

public static class IPAddressLoopbackPolicy
{
    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
