namespace AuswertungPro.Next.UI.Tests;

public sealed class StaTestRunnerTests
{
    [Fact]
    public void Standardzeitlimit_laesst_Lastschwankungen_zu()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), StaTestRunner.DefaultTimeout);
    }
}
