using System;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.UI.Services;

public static class CodingSessionServiceFactory
{
    public static ICodingSessionService Create(
        AppSettings? settings = null,
        ITrainingSampleStore? trainingSamples = null)
        => Create(
            () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig(),
            () => EvalContaminationSetProvider.Load(settings),
            trainingSamples);

    public static ICodingSessionService Create(
        Func<OllamaConfig?> ollamaConfigProvider,
        Func<EvalContaminationSets> evalSetsProvider,
        ITrainingSampleStore? trainingSamples = null)
    {
        ArgumentNullException.ThrowIfNull(ollamaConfigProvider);
        ArgumentNullException.ThrowIfNull(evalSetsProvider);

        var evalSets = new Lazy<EvalContaminationSets>(
            evalSetsProvider,
            LazyThreadSafetyMode.ExecutionAndPublication);

        return new CodingSessionService(
            ollamaConfigProvider,
            () => evalSets.Value.ImageHashes,
            () => evalSets.Value.HaltungKeys,
            trainingSamples);
    }
}
