using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProjectPersistenceServiceFactory
{
    public static CodingProjectPersistenceService Create()
        => new(
            record => PlayerShellProjectServiceFactory.Create().MarkProjectDirty(record),
            () => PlayerShellProjectServiceFactory.Create().TrySaveProjectIfReady(),
            () => PlayerClock.UtcNow());
}
