using System.Globalization;
using System.Windows;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 6: Ohne gewaehlte Zeile liefert WPF fuer jede Feldbindung
/// <see cref="DependencyProperty.UnsetValue"/>. Die beiden Eckdaten-Konverter machten daraus
/// den sichtbaren Fehltext "{DependencyProperty.UnsetValue}" (Pascals Bild vom 07.09., DN /
/// Profil). Sie liefern jetzt denselben Gedankenstrich wie fuer einen leeren Wert.
/// </summary>
public sealed class HaltungFaktenTextConverterTests
{
    [Fact]
    public void Ein_nicht_gesetzter_Wert_wird_zum_Gedankenstrich()
    {
        var konverter = new FaktWertConverter();

        Assert.Equal(
            HaltungFaktenText.Leer,
            konverter.Convert(DependencyProperty.UnsetValue, typeof(string), null!, CultureInfo.InvariantCulture));
        Assert.Equal(
            HaltungFaktenText.Leer,
            konverter.Convert(DependencyProperty.UnsetValue, typeof(string), "m", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Ein_gefuellter_Wert_bleibt_mit_seiner_Einheit()
        => Assert.Equal(
            "30 m",
            new FaktWertConverter().Convert("30", typeof(string), "m", CultureInfo.InvariantCulture));

    [Fact]
    public void Mehrere_nicht_gesetzte_Werte_ergeben_den_Gedankenstrich()
        => Assert.Equal(
            HaltungFaktenText.Leer,
            new FaktZusammenConverter().Convert(
                [DependencyProperty.UnsetValue, DependencyProperty.UnsetValue],
                typeof(string),
                null!,
                CultureInfo.InvariantCulture));

    [Fact]
    public void Ein_gesetzter_Wert_neben_einem_nicht_gesetzten_bleibt_sichtbar()
        => Assert.Equal(
            "300",
            new FaktZusammenConverter().Convert(
                ["300", DependencyProperty.UnsetValue],
                typeof(string),
                null!,
                CultureInfo.InvariantCulture));
}
