using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 3: Einzeilige Zeilen ueberall dort, wo keine lange Textspalte steht.
/// Anlass ist Pascals Bild vom 07.09.: In "Alle Spalten" waren nur sechs Haltungen sichtbar.
/// </summary>
public sealed class DataPageZeilenhoehePolicyTests
{
    [Theory]
    [InlineData("kompakt")]
    [InlineData("stammdaten")]
    [InlineData("sanierung")]
    [InlineData("kosten")]
    public void Ansichten_ohne_lange_Textspalte_sind_einzeilig(string schluessel)
        => Assert.True(DataPageZeilenhoehePolicy.IstEinzeilig(schluessel));

    [Theory]
    [InlineData("alle")]
    [InlineData("ALLE")]
    [InlineData("bewertung")]
    public void Alle_Spalten_und_Bewertung_bleiben_auf_Auto(string schluessel)
        => Assert.False(DataPageZeilenhoehePolicy.IstEinzeilig(schluessel));

    /// <summary>Jede Ansicht des Katalogs ist eindeutig entschieden; nichts bleibt ungeregelt.</summary>
    [Fact]
    public void Jede_bekannte_Ansicht_hat_eine_Entscheidung()
    {
        var einzeilig = DataPageColumnViewCatalog.Views
            .Where(v => DataPageZeilenhoehePolicy.IstEinzeilig(v.Key))
            .Select(v => v.Key)
            .ToArray();

        Assert.Equal(new[] { "kompakt", "stammdaten", "sanierung", "kosten" }, einzeilig);
    }
}
