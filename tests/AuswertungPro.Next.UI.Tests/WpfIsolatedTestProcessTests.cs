namespace AuswertungPro.Next.UI.Tests;

public sealed class WpfIsolatedTestProcessTests
{
    [Fact]
    public async Task Nicht_vorhandener_Kindtest_liefert_keine_Szenario_Bestaetigung()
    {
        var missingTestName = typeof(WpfIsolatedTestProcessTests).FullName
                              + ".Dieser_Test_existiert_nicht";

        var result = await WpfIsolatedTestProcess.RunAsync(
            missingTestName,
            TimeSpan.FromSeconds(30));

        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.False(result.ChildScenarioCompleted);
    }
}
