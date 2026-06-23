using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionRuntime(
    OllamaClient Client,
    LiveDetectionService Service,
    string VisionModel);

public static class LiveDetectionRuntimeFactory
{
    public static Task<LiveDetectionRuntime> CreateAsync(
        AiRuntimeSettings settings,
        CancellationToken ct = default)
        => CreateAsync(
            settings,
            CreateClient,
            static (client, token) => client.ListModelNamesAsync(token),
            static (client, model) => new LiveDetectionService(client, model),
            ct);

    public static async Task<LiveDetectionRuntime> CreateAsync(
        AiRuntimeSettings settings,
        Func<AiRuntimeSettings, OllamaClient> createClient,
        Func<OllamaClient, CancellationToken, Task<IReadOnlyList<string>>> listModelsAsync,
        Func<OllamaClient, string, LiveDetectionService> createService,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(createClient);
        ArgumentNullException.ThrowIfNull(listModelsAsync);
        ArgumentNullException.ThrowIfNull(createService);

        var client = createClient(settings);
        var visionModel = settings.VisionModel;
        try
        {
            var models = await listModelsAsync(client, ct).ConfigureAwait(false);
            visionModel = VisionModelSelectionPolicy.Select(visionModel, models);
        }
        catch
        {
            // Keep the configured model if Ollama is not reachable while starting Live-KI.
        }

        try
        {
            return new LiveDetectionRuntime(client, createService(client, visionModel), visionModel);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static OllamaClient CreateClient(AiRuntimeSettings settings)
        => new OllamaClient(
            settings.OllamaBaseUri,
            ownedTimeout: ResolveTimeout(settings),
            keepAlive: settings.OllamaKeepAlive,
            numCtx: settings.OllamaNumCtx);

    private static TimeSpan ResolveTimeout(AiRuntimeSettings settings)
        => settings.OllamaRequestTimeout > TimeSpan.Zero
            ? settings.OllamaRequestTimeout
            : TimeSpan.FromMinutes(10);
}
