using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingOsdMeterReadResult(
    double? Meter,
    string? RawReply,
    double? Candidate,
    double? RecentMeter,
    string? Error)
{
    public static CodingOsdMeterReadResult Empty { get; } = new(null, null, null, null, null);

    public static CodingOsdMeterReadResult Accepted(
        double meter,
        string rawReply,
        double? candidate,
        double? recentMeter)
        => new(meter, rawReply, candidate, recentMeter, null);

    public static CodingOsdMeterReadResult Rejected(
        string rawReply,
        double? candidate,
        double? recentMeter)
        => new(null, rawReply, candidate, recentMeter, null);

    public static CodingOsdMeterReadResult Failed(string error)
        => new(null, null, null, null, error);
}

public sealed class CodingOsdMeterService : IDisposable
{
    private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(8);

    private readonly Func<byte[], CancellationToken, Task<string>> _readRawMeterAsync;
    private readonly TimeSpan _readTimeout;
    private readonly IDisposable? _ownedClient;
    private bool _disposed;

    public CodingOsdMeterService(
        Func<byte[], CancellationToken, Task<string>> readRawMeterAsync,
        TimeSpan? readTimeout = null,
        IDisposable? ownedClient = null)
    {
        _readRawMeterAsync = readRawMeterAsync ?? throw new ArgumentNullException(nameof(readRawMeterAsync));
        _readTimeout = readTimeout.GetValueOrDefault(DefaultReadTimeout);
        _ownedClient = ownedClient;
    }

    public static CodingOsdMeterService CreateDefault()
    {
        var config = new AppSettingsAiSettingsProvider()
            .Load()
            .ToRuntimeSettings();
        var client = new OllamaClient(
            config.OllamaBaseUri,
            ownedTimeout: config.OllamaRequestTimeout,
            keepAlive: config.OllamaKeepAlive,
            numCtx: config.OllamaNumCtx);

        return new CodingOsdMeterService(
            async (searchImageBytes, ct) =>
            {
                var b64 = Convert.ToBase64String(searchImageBytes);
                var messages = new[]
                {
                    new OllamaClient.ChatMessage("user", CodingOsdMeterReader.Prompt, new[] { b64 })
                };
                return await client.ChatAsync(config.VisionModel, messages, ct).ConfigureAwait(false);
            },
            DefaultReadTimeout,
            client);
    }

    public async Task<CodingOsdMeterReadResult> ReadMeterAsync(
        byte[] pngBytes,
        double? frameTimestampSec,
        double? recentOsdMeter,
        double? recentOsdTimestampSec,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (pngBytes.Length == 0)
            return CodingOsdMeterReadResult.Empty;

        try
        {
            var searchImageBytes = CodingOsdMeterReader.BuildOsdSearchImage(pngBytes);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_readTimeout);

            var raw = await _readRawMeterAsync(searchImageBytes, cts.Token).ConfigureAwait(false);
            var candidate = CodingOsdMeterReader.ParseMeterReply(raw);
            var recentForJumpGuard = recentOsdMeter;
            if (recentForJumpGuard.HasValue
                && CodingMeterResolver.ShouldResetRecentMeterForSeek(frameTimestampSec, recentOsdTimestampSec))
            {
                recentForJumpGuard = null;
            }

            var meter = CodingOsdMeterReader.AcceptMeterCandidate(candidate, recentForJumpGuard);
            return meter.HasValue
                ? CodingOsdMeterReadResult.Accepted(meter.Value, raw, candidate, recentForJumpGuard)
                : CodingOsdMeterReadResult.Rejected(raw, candidate, recentForJumpGuard);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return CodingOsdMeterReadResult.Empty;
        }
        catch (Exception ex)
        {
            return CodingOsdMeterReadResult.Failed(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _ownedClient?.Dispose();
        _disposed = true;
    }
}
