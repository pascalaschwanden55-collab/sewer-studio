using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using SidecarE2eSmoke;

namespace NightlySoakRunner;

public sealed class SidecarRunSession : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly SidecarProcessLease _lease;

    private SidecarRunSession(HttpClient http, SidecarProcessLease lease)
    {
        _http = http;
        _lease = lease;
    }

    public static async Task<SidecarRunSession> StartAsync(
        NightlySoakOptions options,
        CancellationToken ct)
    {
        var smoke = options.CreateSmokeOptions(options.VideoPaths[0], options.StartSidecar);
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(options.StartupTimeoutSeconds) };
        try
        {
            var client = new VisionPipelineClient(new Uri(options.SidecarUrl), http, options.Token);
            var lease = await SidecarProcessLease.EnsureReadyAsync(smoke, client, ct);
            return new SidecarRunSession(http, lease);
        }
        catch
        {
            http.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lease.DisposeAsync();
        _http.Dispose();
    }
}
