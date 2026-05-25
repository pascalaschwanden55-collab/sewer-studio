using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Services;

public sealed class ProtocolTrainingSampleProvider : IProtocolAiTrainingSampleProvider
{
    public IReadOnlyList<ProtocolAiTrainingSample> LoadRecent(int maxCount) =>
        ProtocolTrainingStore.LoadRecent(maxCount)
            .Select(sample => new ProtocolAiTrainingSample(
                AtUtc: sample.AtUtc,
                HaltungId: sample.HaltungId,
                Code: sample.Code,
                Beschreibung: sample.Beschreibung,
                MeterStart: sample.MeterStart,
                MeterEnd: sample.MeterEnd,
                IsStreckenschaden: sample.IsStreckenschaden,
                Parameters: new Dictionary<string, string>(
                    sample.Parameters,
                    StringComparer.OrdinalIgnoreCase)))
            .ToList();
}
