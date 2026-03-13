using System;
using System.Net.Http;
using System.Reflection;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OllamaClientTests
{
    [Fact]
    public void Constructor_OwnedClient_UsesConfiguredTimeout()
    {
        var uri = new Uri("http://localhost:11434");
        var client = new OllamaClient(uri, ownedTimeout: TimeSpan.FromMinutes(42));

        var http = ExtractHttpClient(client);

        Assert.Equal(uri, http.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(42), http.Timeout);
    }

    [Fact]
    public void Constructor_ProvidedClient_PreservesExistingTimeout()
    {
        var uri = new Uri("http://localhost:11434");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        _ = new OllamaClient(uri, http, TimeSpan.FromMinutes(42));

        // BaseAddress wird bei bereitgestelltem HttpClient NICHT gesetzt
        // (nur bei owned clients), daher bleibt der Originalwert erhalten.
        Assert.Null(http.BaseAddress);
        Assert.Equal(TimeSpan.FromMinutes(3), http.Timeout);
    }

    private static HttpClient ExtractHttpClient(OllamaClient client)
    {
        var field = typeof(OllamaClient).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<HttpClient>(field!.GetValue(client));
    }
}
