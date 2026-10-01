using System.IO;
using System.Reflection;

using AuswertungPro.Next.UI.ViewModels.Pages;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierComponentListActionsTests
{
    private static string PageXaml() => File.ReadAllText(RepoFile(
        "src", "AuswertungPro.Next.UI", "Views", "Pages", "DossiersPage.xaml"));

    [Theory]
    [InlineData("Haltungsliste erstellen", "CreateHoldingListCommand")]
    [InlineData("Schachtliste erstellen", "CreateShaftListCommand")]
    public void Ausgabe_bietet_beide_bewussten_Listenaktionen_an(
        string content,
        string command)
    {
        var xaml = PageXaml();
        var button = xaml.IndexOf($"Content=\"{content}\"", StringComparison.Ordinal);

        Assert.True(button >= 0, $"Der Knopf '{content}' fehlt.");
        var section = xaml[button..Math.Min(xaml.Length, button + 650)];
        Assert.Contains($"Command=\"{{Binding {command}}}\"", section, StringComparison.Ordinal);
        Assert.Contains("Erst nach den Korrekturen erzeugen", section, StringComparison.Ordinal);
        Assert.Contains("wird nicht überschrieben", section, StringComparison.Ordinal);
        Assert.Contains("freier Dateiname", section, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CreateHoldingListCommand")]
    [InlineData("CreateShaftListCommand")]
    public void Cockpit_stellt_den_Listenbefehl_bereit(string propertyName)
    {
        var property = typeof(DossiersPageViewModel)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(property);
        Assert.True(typeof(System.Windows.Input.ICommand).IsAssignableFrom(property!.PropertyType));
    }
}
