using AuswertungPro.Next.UI.LiveControl;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

// Tests fuer die schmale Bruecke zwischen Live-Control-Server und der DataPage.
public sealed class LiveControlRetryBridgeTests : IDisposable
{
    public void Dispose() => LiveControlRetryBridge.Reset();

    [Fact]
    public void Invoke_OhneHandler_MeldetDatenseiteNichtOffen()
    {
        LiveControlRetryBridge.Reset();

        var result = LiveControlRetryBridge.Invoke("06.24341-35625");

        Assert.False(result.Ok);
        Assert.Contains("Datenseite", result.Message);
    }

    [Fact]
    public void Invoke_LeererName_MeldetFehlt()
    {
        LiveControlRetryBridge.Register(_ => new LiveControlRetryResult(true, "sollte nicht aufgerufen werden"));

        var result = LiveControlRetryBridge.Invoke("   ");

        Assert.False(result.Ok);
        Assert.Contains("fehlt", result.Message);
    }

    [Fact]
    public void Invoke_MitHandler_RuftHandlerMitNameAuf()
    {
        string? gesehen = null;
        LiveControlRetryBridge.Register(name =>
        {
            gesehen = name;
            return new LiveControlRetryResult(true, $"Analyse fuer '{name}' gestartet.");
        });

        var result = LiveControlRetryBridge.Invoke("06.24341-35625");

        Assert.Equal("06.24341-35625", gesehen);
        Assert.True(result.Ok);
        Assert.Contains("gestartet", result.Message);
    }

    [Fact]
    public void Reset_EntferntHandler()
    {
        LiveControlRetryBridge.Register(_ => new LiveControlRetryResult(true, "x"));
        LiveControlRetryBridge.Reset();

        var result = LiveControlRetryBridge.Invoke("06.24341-35625");

        Assert.False(result.Ok);
    }
}
