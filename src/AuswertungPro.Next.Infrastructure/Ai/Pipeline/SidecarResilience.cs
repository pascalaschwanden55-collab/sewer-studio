using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Sprint 1 (2026-05-07): Polly v8 ResiliencePipeline fuer Sidecar-HTTP-Calls.
///
/// Im Gegensatz zum Ollama-Client gibt es hier KEIN Retry — Sidecar-Endpoints
/// (POST /detect, POST /segment) sind nicht idempotent + frisch retry'n waere
/// teuer (GPU). Stattdessen nur Circuit-Breaker: wenn der Sidecar wiederholt
/// fehlschlaegt, oeffnet der Breaker und alle weiteren Calls werfen sofort
/// <see cref="BrokenCircuitException"/> — schneller Fallback statt 15 s Timeout
/// pro Call.
///
/// Schwellen:
/// - 50 % Fehlerquote in 60 s Sampling-Fenster
/// - Mindestens 5 Calls fuer ein Urteil
/// - Bei OPEN: 30 s Sperrzeit, dann HALF-OPEN-Probe
/// </summary>
public static class SidecarResilience
{
    /// <summary>
    /// Erstellt eine neue ResiliencePipeline fuer Sidecar-Calls.
    /// Optional <paramref name="onOpened"/>/<paramref name="onClosed"/>-Callbacks
    /// werden aufgerufen wenn der Breaker den Status wechselt (z.B. fuer Logging).
    /// </summary>
    public static ResiliencePipeline CreateCircuitBreaker(
        Action? onOpened = null,
        Action? onClosed = null)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested),
                OnOpened = args =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Sidecar-Resilience] Circuit OPEN ({args.BreakDuration.TotalSeconds:F0} s) — Sidecar nicht erreichbar");
                    onOpened?.Invoke();
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    System.Diagnostics.Debug.WriteLine("[Sidecar-Resilience] Circuit CLOSED — Sidecar wieder verfuegbar");
                    onClosed?.Invoke();
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}

/// <summary>
/// DelegatingHandler der jeden HTTP-Call durch eine Polly-Pipeline schickt.
/// Wird in den HttpClient eingehaengt und ist fuer die Aufrufer transparent.
/// </summary>
public sealed class SidecarResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline _pipeline;

    public SidecarResilienceHandler(ResiliencePipeline pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        System.Threading.CancellationToken cancellationToken)
    {
        // Pipeline mit Circuit-Breaker only (kein Retry) — HttpRequestMessage darf einmal gesendet werden.
        return await _pipeline.ExecuteAsync(
            async token => await base.SendAsync(request, token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }
}
