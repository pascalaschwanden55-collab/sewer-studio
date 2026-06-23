using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingPrimaryDamageSynchronizerFactory
{
    public static CodingPrimaryDamageSynchronizer Create()
        => new(CodingPrimaryDamageTextBuilder.Build, () => PlayerClock.UtcNow());
}
