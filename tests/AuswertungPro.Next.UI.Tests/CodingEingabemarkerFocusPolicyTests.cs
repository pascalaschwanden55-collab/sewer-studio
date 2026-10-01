using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerFocusPolicyTests
{
    [Fact]
    public void Nach_einer_Schnellauswahl_gehoert_der_Schreibfokus_ins_Textfeld()
    {
        // Sonst bleibt der Fokus auf der Auswahlliste: Enter bestaetigt dort nicht,
        // und der Benutzer muss erst ins Textfeld klicken.
        Assert.True(CodingEingabemarkerFocusPolicy.ShouldFocusInput(popupVisible: true, selectedText: "Riss"));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(false, "Riss")]
    public void Ohne_echte_Auswahl_oder_bei_geschlossenem_Feld_wird_kein_Fokus_gestohlen(
        bool popupVisible,
        string? selectedText)
    {
        // Das Zuruecksetzen der Liste beim Oeffnen (SelectedIndex = -1) darf keinen
        // Fokuswechsel ausloesen.
        Assert.False(CodingEingabemarkerFocusPolicy.ShouldFocusInput(popupVisible, selectedText));
    }
}
