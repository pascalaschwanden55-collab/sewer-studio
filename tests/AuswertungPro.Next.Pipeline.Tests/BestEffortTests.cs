using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection("BestEffort global sink")]
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
        // Kein Sink -> Release-tauglicher Trace-Rueckfall. Darf NICHT werfen.
        var ex = Record.Exception(() => BestEffort.Try(() => throw new Exception("x"), "ctx"));
        Assert.Null(ex);
    }

    [Fact]
    public void Try_OhneAufrufSink_NutztKonfiguriertenTageslogSink()
    {
        string? captured = null;
        BestEffort.ConfigureDefaultErrorSink(message => captured = message);
        try
        {
            BestEffort.Try(() => throw new IOException("Datei gesperrt"), "Backup aufraeumen");
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
        }

        Assert.NotNull(captured);
        Assert.Contains("Backup aufraeumen", captured);
        Assert.Contains("IOException", captured);
    }

    [Fact]
    public void ReportWarning_NutztKonfiguriertenTageslogSink()
    {
        string? captured = null;
        BestEffort.ConfigureDefaultErrorSink(message => captured = message);
        try
        {
            BestEffort.ReportWarning("Trainingsdaten korrupt");
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
        }

        Assert.Equal("Trainingsdaten korrupt", captured);
    }

    [Fact]
    public void FehlerImTageslogSink_BrichtHauptablaufNichtAb()
    {
        BestEffort.ConfigureDefaultErrorSink(_ => throw new IOException("Log gesperrt"));
        try
        {
            var ex = Record.Exception(() =>
                BestEffort.Try(() => throw new InvalidOperationException("Cleanup"), "Test"));
            Assert.Null(ex);
        }
        finally
        {
            BestEffort.ConfigureDefaultErrorSink(null);
        }
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

[CollectionDefinition("BestEffort global sink", DisableParallelization = true)]
public sealed class BestEffortGlobalSinkCollection;
