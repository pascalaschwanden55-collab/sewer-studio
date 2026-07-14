using System;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Services;

public sealed class ProtocolTrainingSampleProvider : IProtocolAiTrainingSampleProvider
{
    private readonly IProtocolTrainingStore _store;

    public ProtocolTrainingSampleProvider()
        : this(ProtocolTrainingStore.Current)
    {
    }

    public ProtocolTrainingSampleProvider(IProtocolTrainingStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IReadOnlyList<ProtocolAiTrainingSample> LoadRecent(int maxCount) =>
        _store.LoadRecent(maxCount);
}
