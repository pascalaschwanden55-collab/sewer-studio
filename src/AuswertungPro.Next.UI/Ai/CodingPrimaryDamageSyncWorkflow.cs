using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingPrimaryDamageSyncWorkflowActions(
    Func<CodingPrimaryDamageSynchronizer> CreateSynchronizer);

public static class CodingPrimaryDamageSyncWorkflow
{
    public static void Sync(
        HaltungRecord record,
        ProtocolDocument document)
        => Sync(
            record,
            document,
            new CodingPrimaryDamageSyncWorkflowActions(
                CreateSynchronizer: CodingPrimaryDamageSynchronizerFactory.Create));

    public static void Sync(
        HaltungRecord record,
        ProtocolDocument document,
        CodingPrimaryDamageSyncWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateSynchronizer);

        var synchronizer = actions.CreateSynchronizer();
        ArgumentNullException.ThrowIfNull(synchronizer);

        synchronizer.Sync(record, document);
    }
}
