using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class BestEffortTests
{
    [Fact]
    public void Try_ErfolgreicheAktion_RuftSinkNicht()
    {
        string? captured = null;
        BestEffort.Try(() => { /* ok */ }, "ctx", m => captured = m);
        Assert.Null(captured);
    }

    [Fact]
    public void Try_FehlerhafteAktion_MeldetKontextUndWirftNicht()
    {
        string? captured = null;
        BestEffort.Try(() => throw new InvalidOperationException("kaputt"), "Cleanup X", m => captured = m);

        Assert.NotNull(captured);
        Assert.Contains("Cleanup X", captured);
        Assert.Contains("InvalidOperationException", captured);
        Assert.Contains("kaputt", captured);
    }

    [Fact]
    public void Try_OhneSink_VerschlucktNichtMehrStill_WirftAberAuchNicht()
    {
        // Kein Sink -> Default (Debug). Darf NICHT werfen.
        var ex = Record.Exception(() => BestEffort.Try(() => throw new Exception("x"), "ctx"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task TryAsync_FehlerhafteAktion_MeldetKontextUndWirftNicht()
    {
        string? captured = null;
        await BestEffort.TryAsync(() => throw new TimeoutException("zu lang"), "Async Y", m => captured = m);

        Assert.NotNull(captured);
        Assert.Contains("Async Y", captured);
        Assert.Contains("TimeoutException", captured);
    }

    [Fact]
    public async Task TryAsync_ErfolgreicheAktion_RuftSinkNicht()
    {
        string? captured = null;
        await BestEffort.TryAsync(() => Task.CompletedTask, "ctx", m => captured = m);
        Assert.Null(captured);
    }
}
