using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Baut die Schnellscan-Pipeline (ffmpeg-Pfad, eigener Ollama-Client, QuickScanService)
/// aus den Laufzeit-Einstellungen und haelt den Ollama-Client als eigene Ressource.
/// Fasst den frueher im UI-Controller inline aufgebauten und dort dispose-pflichtigen
/// Client an einer testbaren Infrastructure-Stelle zusammen.
/// </summary>
public sealed class QuickScanSession : IQuickScanSession
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private readonly OllamaClient _client;

    public IQuickScanService Service { get; }

    private QuickScanSession(OllamaClient client, IQuickScanService service)
    {
        _client = client;
        Service = service;
    }

    public static QuickScanSession Create(AiRuntimeSettings cfg, IProcessOutputReader processOutputs)
    {
        ArgumentNullException.ThrowIfNull(cfg);

        var ffmpegPath = cfg.FfmpegPath ?? FfmpegLocator.ResolveFfmpeg();
        var client = new OllamaClient(
            cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : DefaultTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx);

        var service = new QuickScanService(client, cfg.VisionModel, ffmpegPath, processOutputs);
        return new QuickScanSession(client, service);
    }

    public void Dispose() => _client.Dispose();
}
