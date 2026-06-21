using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Services;

public sealed record EvalContaminationSets(
    IReadOnlySet<string> ImageHashes,
    IReadOnlySet<string> HaltungKeys);

public static class EvalContaminationSetProvider
{
    public static EvalContaminationSets Load(AppSettings? settings)
        => Load(settings?.EvalSetRoot ?? AppSettings.Load().EvalSetRoot);

    public static EvalContaminationSets Load(string? evalSetRoot)
        => new(
            EvalContaminationGuard.LoadEvalImageHashes(evalSetRoot),
            EvalContaminationGuard.LoadEvalHaltungKeys(evalSetRoot));
}
