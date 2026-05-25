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
        var item = new ShellViewModel.NavItem("", title, () => new object());

        Assert.True(ShellNavigationPolicy.CanOpenWithoutProject(title));
        Assert.False(ShellNavigationPolicy.RequiresProject(title));
        Assert.True(item.CanOpenWithoutProject);
        Assert.False(item.RequiresProject);

        item.UpdateAvailability(isProjectReady: false);
        Assert.True(item.IsAvailable);
        Assert.Equal(1.0, item.AvailabilityOpacity);
    }

    [Theory]
    [InlineData("Haltungen")]
    [InlineData("Schaechte")]
    [InlineData("Import")]
    [InlineData("VSA")]
    public void DataPagesStillRequireProject(string title)
    {
        var item = new ShellViewModel.NavItem("", title, () => new object());

        Assert.False(ShellNavigationPolicy.CanOpenWithoutProject(title));
        Assert.True(ShellNavigationPolicy.RequiresProject(title));
        Assert.False(item.CanOpenWithoutProject);
        Assert.True(item.RequiresProject);

        item.UpdateAvailability(isProjectReady: false);
        Assert.False(item.IsAvailable);
        Assert.Equal(0.5, item.AvailabilityOpacity);

        item.UpdateAvailability(isProjectReady: true);
        Assert.True(item.IsAvailable);
        Assert.Equal(1.0, item.AvailabilityOpacity);
    }

    [Fact]
    public void NavItemCanBeExplicitlyAvailableWithoutProject()
    {
        var item = new ShellViewModel.NavItem("", "Custom", () => new object(), canOpenWithoutProject: true);

        item.UpdateAvailability(isProjectReady: false);

        Assert.True(item.CanOpenWithoutProject);
        Assert.False(item.RequiresProject);
        Assert.True(item.IsAvailable);
        Assert.Equal(1.0, item.AvailabilityOpacity);
    }
}
