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

    /// <summary>
    /// Fix-Runde 1 (Minor): Eine unbekannte oder fehlende Ansicht bleibt auf Auto. Lieber eine
    /// zu hohe Zeile als ein abgeschnittener Wert.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gibt-es-nicht")]
    public void Unbekannte_Ansichten_bleiben_auf_Auto(string? schluessel)
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

    /// <summary>
    /// Nova-Fixwelle 2b (P2): Die frei einstellbare Mindesthoehe (Werkseinstellung 38) war
    /// groesser als die kompakte Zeilenhoehe und hat das Token vollstaendig ausgehebelt —
    /// gemessen blieb die Zeile bei 38 px, egal was in RowHeightCompact stand.
    /// </summary>
    [Fact]
    public void In_einer_einzeiligen_Ansicht_gewinnt_die_kompakte_Zeilenhoehe()
        => Assert.Equal(34d, DataPageZeilenhoehePolicy.Mindesthoehe(einzeilig: true, kompakt: 34d, eingestellt: 38d));

    /// <summary>Eine bewusst kleiner eingestellte Mindesthoehe bleibt erhalten.</summary>
    [Fact]
    public void Eine_kleinere_Einstellung_bleibt_erhalten()
        => Assert.Equal(28d, DataPageZeilenhoehePolicy.Mindesthoehe(einzeilig: true, kompakt: 34d, eingestellt: 28d));

    [Fact]
    public void Ausserhalb_der_einzeiligen_Ansichten_gilt_allein_die_Einstellung()
        => Assert.Equal(50d, DataPageZeilenhoehePolicy.Mindesthoehe(einzeilig: false, kompakt: 34d, eingestellt: 50d));

    /// <summary>Ohne lesbares Token wird die Einstellung nicht auf 0 gesenkt.</summary>
    [Fact]
    public void Ohne_gueltiges_Token_bleibt_die_Einstellung_stehen()
        => Assert.Equal(38d, DataPageZeilenhoehePolicy.Mindesthoehe(einzeilig: true, kompakt: 0d, eingestellt: 38d));
}
