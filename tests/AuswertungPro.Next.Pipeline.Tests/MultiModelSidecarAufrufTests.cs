using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der gemeinsame Aufrufhelfer der Modellschritte (AP05b) ordnet nur die Fehlerart ein:
/// Nutzerabbruch geht durch, VRAM-Mangel ist Kapazitaet, alles andere - auch ein interner
/// Zeitablauf ohne Nutzerabbruch - ist Transport.
/// </summary>
public sealed class MultiModelSidecarAufrufTests
{
    [Fact]
    public async Task Erfolg_liefert_die_Antwort_ohne_Fehlerart()
    {
        var ausgang = await MultiModelSidecarAufruf.AusfuehrenAsync(() => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", ausgang.Antwort);
        Assert.Equal(MultiModelFehlerart.Keine, ausgang.Fehlerart);
        Assert.Null(ausgang.Fehler);
    }

    [Fact]
    public async Task Vram_Mangel_ist_Kapazitaet()
    {
        var vram = new SidecarInsufficientVramException("/segment/sam", 1.5, 6.0, 2.0);

        var ausgang = await MultiModelSidecarAufruf.AusfuehrenAsync<string>(() => throw vram, CancellationToken.None);

        Assert.Equal(MultiModelFehlerart.Kapazitaet, ausgang.Fehlerart);
        Assert.Same(vram, ausgang.Fehler);
        Assert.Null(ausgang.Antwort);
    }

    [Fact]
    public async Task Transportfehler_und_interner_Zeitablauf_sind_Transport()
    {
        var transport = await MultiModelSidecarAufruf.AusfuehrenAsync<string>(
            async () => { await Task.Yield(); throw new HttpRequestException("weg"); }, CancellationToken.None);
        var zeitablauf = await MultiModelSidecarAufruf.AusfuehrenAsync<string>(
            () => throw new TaskCanceledException("HttpClient-Timeout"), CancellationToken.None);

        Assert.Equal(MultiModelFehlerart.Transport, transport.Fehlerart);
        Assert.IsType<HttpRequestException>(transport.Fehler);
        Assert.Equal(MultiModelFehlerart.Transport, zeitablauf.Fehlerart);
    }

    [Fact]
    public async Task Nutzerabbruch_wird_weitergeworfen()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MultiModelSidecarAufruf.AusfuehrenAsync<string>(
            () => throw new OperationCanceledException(cts.Token), cts.Token));
    }
}
