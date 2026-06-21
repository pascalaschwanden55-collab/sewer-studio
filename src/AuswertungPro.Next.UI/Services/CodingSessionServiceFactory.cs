using System;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.UI.Services;

public static class CodingSessionServiceFactory
{
    public static ICodingSessionService Create(AppSettings? settings = null)
        => Create(
            () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig(),
            () => EvalContaminationSetProvider.Load(settings));

    public static ICodingSessionService Create(
        Func<OllamaConfig?> ollamaConfigProvider,
        Func<EvalContaminationSets> evalSetsProvider)
    {
        ArgumentNullException.ThrowIfNull(ollamaConfigProvider);
        ArgumentNullException.ThrowIfNull(evalSetsProvider);

        var evalSets = new Lazy<EvalContaminationSets>(
            evalSetsProvider,
            LazyThreadSafetyMode.ExecutionAndPublication);

        return new CodingSessionService(
            ollamaConfigProvider,
            () => evalSets.Value.ImageHashes,
            () => evalSets.Value.HaltungKeys);
    }
}
