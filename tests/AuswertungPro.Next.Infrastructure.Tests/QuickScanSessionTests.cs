using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert die aus dem UI-Controller ausgelagerte Schnellscan-Pipeline: die Session
/// baut den Dienst und haelt den Ollama-Client, ohne dass ein UI-Controller ihn newt.
/// </summary>
public sealed class QuickScanSessionTests
{
    private static AiRuntimeSettings Cfg() => new(
        Enabled: true,
        OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
        VisionModel: "vision",
        TextModel: "text",
        EmbedModel: null,
        FfmpegPath: "ffmpeg",   // gesetzt -> FfmpegLocator wird nicht befragt
        OllamaRequestTimeout: TimeSpan.FromSeconds(1),
        OllamaKeepAlive: "5m",
        OllamaNumCtx: 1024);

    [Fact]
    public void Create_liefert_Session_mit_QuickScanService()
    {
        using var session = QuickScanSession.Create(Cfg(), ProcessOutputReader.Current);

        Assert.NotNull(session.Service);
        Assert.IsType<QuickScanService>(session.Service);
    }

    [Fact]
    public void Dispose_ist_idempotent()
    {
        var session = QuickScanSession.Create(Cfg(), ProcessOutputReader.Current);

        session.Dispose();
        session.Dispose();   // zweiter Dispose des eigenen Ollama-Clients darf nicht werfen
    }
}
