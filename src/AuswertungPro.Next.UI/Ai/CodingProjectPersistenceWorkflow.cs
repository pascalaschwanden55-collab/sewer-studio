using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingProjectPersistenceWorkflowActions(
    Func<CodingProjectPersistenceService> CreateService);

public static class CodingProjectPersistenceWorkflow
{
    public static void MarkProjectDirty(
        HaltungRecord? record,
        CodingProjectPersistenceWorkflowActions actions)
    {
        var service = Create(actions);

        service.MarkProjectDirty(record);
    }

    public static void TrySaveProjectIfReady(
        CodingProjectPersistenceWorkflowActions actions)
    {
        var service = Create(actions);

        service.TrySaveProjectIfReady();
    }

    private static CodingProjectPersistenceService Create(
        CodingProjectPersistenceWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        return service;
    }
}
