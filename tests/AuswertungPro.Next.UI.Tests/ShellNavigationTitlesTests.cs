using AuswertungPro.Next.UI.ViewModels;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellNavigationTitlesTests
{
    [Theory]
    [InlineData("Uebersicht", "Übersicht")]
    [InlineData("Schaechte", "Schächte")]
    [InlineData("Haltungen", "Haltungen")]
    [InlineData("Sanierungs-Matrix", "Sanierungs-Matrix")]
    public void Anzeigename_traegt_echte_Umlaute(string title, string erwartet)
        => Assert.Equal(erwartet, ShellNavigationTitles.Anzeige(title));
}
