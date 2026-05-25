using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ShellNavigationPolicyTests
{
    [Theory]
    [InlineData("Uebersicht")]
    [InlineData("Projekt")]
    [InlineData("Export")]
    [InlineData("Einstellungen")]
    public void CorePagesStayAvailableWithoutProject(string title)
    {
        Assert.False(ShellNavigationPolicy.RequiresProject(title));
    }

    [Theory]
    [InlineData("Haltungen")]
    [InlineData("Schaechte")]
    [InlineData("Import")]
    [InlineData("VSA")]
    public void DataPagesStillRequireProject(string title)
    {
        Assert.True(ShellNavigationPolicy.RequiresProject(title));
    }
}
