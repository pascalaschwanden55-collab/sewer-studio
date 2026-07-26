using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// 5s-Polling des Sidecar-Health. Mappt das Client-Ergebnis auf <see cref="PipelineHealthInputs"/>,
/// wertet via <see cref="PipelineHealthEvaluator"/> aus und feuert <see cref="StatusChanged"/> bei Aenderung.
/// </summary>
public sealed class PipelineHealthMonitor : IPipelineHealthMonitor
{
    private readonly IVisionPipelineClient _client;
    private readonly Func<bool> _aiEnabled;
    private readonly Func<bool> _qwenAvailable;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PipelineHealthMonitor(
        IVisionPipelineClient client,
        Func<bool> aiEnabled,
        Func<bool> qwenAvailable,
        TimeSpan? interval = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _aiEnabled = aiEnabled ?? (() => true);
        _qwenAvailable = qwenAvailable ?? (() => true);
        _interval = interval ?? TimeSpan.FromSeconds(5);
        CurrentStatus = PipelineHealthEvaluator.Evaluate(
            new PipelineHealthInputs(true, false, false, false, _qwenAvailable()));
    }

    public PipelineHealthStatus CurrentStatus { get; private set; }
    public event EventHandler<PipelineHealthStatus>? StatusChanged;

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RefreshOnceAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { /* nie crashen; naechster Tick */ }

            try { await Task.Delay(_interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default)
    {
        bool ai = _aiEnabled();
        bool qwen = _qwenAvailable();

        PipelineHealthInputs inputs;
        if (!ai)
        {
            inputs = new PipelineHealthInputs(false, false, false, false, qwen);
        }
        else
        {
            var r = await _client.CheckHealthDetailedAsync(ct).ConfigureAwait(false);
            // Fehlender Klassifikator ("degraded" nur deswegen) ist eine Warnung, kein
            // Blocker: die Ampel bleibt Full, DINO/SAM bleiben harte Anforderungen.
            bool healthy = r.Error is null
                           && r.Health is { } health
                           && health.HasRequiredModels
                           && (string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase)
                               || health.ClassifierMissing
                               || health.DetectorQualification is { Qualified: false });
            var loaded = r.Health?.Gpu?.LoadedModels;
            bool Has(string k) => loaded != null
                && loaded.Keys.Any(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
            inputs = new PipelineHealthInputs(
                AiEnabled: true,
                SidecarReachable: r.IsReachable,
                TokenValid: r.IsAuthorized,
                SidecarHealthy: healthy,
                QwenAvailable: qwen,
                YoloLoaded: Has("yolo"),
                DinoLoaded: Has("dino"),
                SamLoaded: Has("sam"),
                DetectorQualified: r.Health?.DetectorQualification?.Qualified,
                DetectorQualificationReason: r.Health?.DetectorQualification?.Reason);
        }

        var status = PipelineHealthEvaluator.Evaluate(inputs);
        if (status != CurrentStatus)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(this, status);
        }
        return status;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        if (_loop is not null) { try { await _loop.ConfigureAwait(false); } catch { /* erwartet bei Cancel */ } }
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
