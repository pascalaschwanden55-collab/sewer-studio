using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterWindowLifetimeTests
{
    [Fact]
    public void Dispose_bricht_laufende_Arbeit_ab_und_ist_wiederholbar()
    {
        var lifetime = new TrainingCenterWindowLifetime();
        var token = lifetime.Token;

        lifetime.Dispose();
        lifetime.Dispose();

        Assert.True(token.IsCancellationRequested);
    }
}
