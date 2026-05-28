using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai;

public sealed record ProtocolAiTrainingSample(
    DateTime AtUtc,
    string HaltungId,
    string Code,
    string Beschreibung,
    double? MeterStart,
    double? MeterEnd,
    bool IsStreckenschaden,
    IReadOnlyDictionary<string, string> Parameters);

public interface IProtocolAiTrainingSampleProvider
{
    IReadOnlyList<ProtocolAiTrainingSample> LoadRecent(int maxCount);
}

public sealed class NoopProtocolAiTrainingSampleProvider : IProtocolAiTrainingSampleProvider
{
    public static NoopProtocolAiTrainingSampleProvider Instance { get; } = new();

    private NoopProtocolAiTrainingSampleProvider()
    {
    }

    public IReadOnlyList<ProtocolAiTrainingSample> LoadRecent(int maxCount) =>
        Array.Empty<ProtocolAiTrainingSample>();
}
