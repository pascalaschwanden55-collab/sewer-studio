using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Polly.CircuitBreaker;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Tests fuer SidecarResilience (Sprint 1: Circuit-Breaker auf Sidecar).</summary>
public class SidecarResilienceTests
{
    [Fact]
    public async Task CircuitBreaker_HappyPath_PassesThrough()
    {
        var pipeline = SidecarResilience.CreateCircuitBreaker();

        var result = await pipeline.ExecuteAsync(
            async _ => { await Task.Yield(); return 42; },
            CancellationToken.None);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterRepeatedFailures()
    {
        bool opened = false;
        var pipeline = SidecarResilience.CreateCircuitBreaker(
            onOpened: () => opened = true);

        // Mind. 5 Calls fuer Urteil, FailureRatio 0.5 → 5 Failures aus 5 = 100%
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await pipeline.ExecuteAsync<int>(
                    _ => throw new HttpRequestException("simulated"),
                    CancellationToken.None);
            }
            catch (HttpRequestException) { /* expected */ }
        }

        // 6. Call sollte mit BrokenCircuitException sofort failen (oder nochmal Failure)
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync<int>(
                _ => throw new HttpRequestException("simulated"),
                CancellationToken.None);
        });

        Assert.True(opened, "Circuit-Breaker sollte nach 5 Fails OPEN sein");
    }

    [Fact]
    public async Task CircuitBreaker_OperationCanceled_DoesNotCount()
    {
        var pipeline = SidecarResilience.CreateCircuitBreaker();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // OCE mit cancelled-Token: zaehlt nicht als Failure
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await pipeline.ExecuteAsync<int>(
                    token =>
                    {
                        token.ThrowIfCancellationRequested();
                        return ValueTask.FromResult(0);
                    },
                    cts.Token);
            }
            catch (OperationCanceledException) { /* expected */ }
        }

        // Der Breaker darf nicht offen sein — neue Calls sollen funktionieren
        var result = await pipeline.ExecuteAsync(
            async _ => { await Task.Yield(); return 1; },
            CancellationToken.None);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task SidecarResilienceHandler_PassesThroughOnSuccess()
    {
        var stub = new StubHandler(System.Net.HttpStatusCode.OK);
        var handler = new SidecarResilienceHandler(SidecarResilience.CreateCircuitBreaker())
        {
            InnerHandler = stub
        };

        using var client = new HttpClient(handler);
        var resp = await client.GetAsync("http://localhost/test");

        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, stub.CallCount);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        private readonly System.Net.HttpStatusCode _status;

        public StubHandler(System.Net.HttpStatusCode status) { _status = status; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }
}
