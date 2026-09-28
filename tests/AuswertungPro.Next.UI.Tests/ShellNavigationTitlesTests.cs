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
    // Optikanalyse 28.09.2026, Aufgabe 7: Leiste ↔ Seitentitel angleichen (NovaPageHeader
    // "Eigentümerdossiers"/"VSA-Bewertung"); der Schluessel bleibt "Dossiers"/"VSA".
    [InlineData("Dossiers", "Eigentümerdossiers")]
    [InlineData("VSA", "VSA-Bewertung")]
    public void Anzeigename_traegt_echte_Umlaute(string title, string erwartet)
        => Assert.Equal(erwartet, ShellNavigationTitles.Anzeige(title));
}
