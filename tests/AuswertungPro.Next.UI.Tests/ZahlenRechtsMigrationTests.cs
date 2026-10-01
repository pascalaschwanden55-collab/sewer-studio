using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Fixwelle 2b, Runde 2: Einmalige Migration des gespeicherten Spaltenlayouts.
/// „Zahlen rechts" und die Startbreiten wirken nur beim Aufbau; ein bestehendes Layout
/// ueberschrieb beides gleich wieder.
/// </summary>
public sealed class ZahlenRechtsMigrationTests
{
    private static DataPageLayoutSettings Layout(params DataPageColumnLayout[] spalten)
        => new() { Columns = [.. spalten] };

    private static DataPageColumnLayout Spalte(
        string feld, string ausrichtung = "Left", double breite = 72d, string einheit = "Pixel")
        => new() { FieldName = feld, HorizontalAlignment = ausrichtung, WidthValue = breite, WidthUnitType = einheit };

    [Fact]
    public void Beim_ersten_Mal_werden_alle_Zahlenspalten_rechts_ausgerichtet()
    {
        var layout = Layout(
            Spalte(FieldKeys.NominalDiameterMm),
            Spalte(FieldKeys.HoldingLengthMeters),
            Spalte(FieldKeys.Cost),
            Spalte(FieldKeys.Street));

        Assert.True(ZahlenRechtsMigration.Entscheide(layout));

        Assert.Equal("Right", layout.Columns[0].HorizontalAlignment);
        Assert.Equal("Right", layout.Columns[1].HorizontalAlignment);
        Assert.Equal("Right", layout.Columns[2].HorizontalAlignment);
        Assert.Equal("Left", layout.Columns[3].HorizontalAlignment);
        Assert.True(layout.ZahlenRechtsEinmalGesetzt);
    }

    [Fact]
    public void Danach_bleibt_alles_unberuehrt()
    {
        var layout = Layout(Spalte(FieldKeys.NominalDiameterMm));
        layout.ZahlenRechtsEinmalGesetzt = true;

        Assert.False(ZahlenRechtsMigration.Entscheide(layout));
        Assert.Equal("Left", layout.Columns[0].HorizontalAlignment);
    }

    [Fact]
    public void Zu_schmale_Breiten_werden_angehoben()
    {
        var layout = Layout(
            Spalte(FieldKeys.HoldingName, breite: 90d),
            Spalte(FieldKeys.Street, breite: 72d),
            Spalte(FieldKeys.PipeMaterial, breite: 80d));

        ZahlenRechtsMigration.Entscheide(layout);

        Assert.Equal(150d, layout.Columns[0].WidthValue);
        Assert.Equal(120d, layout.Columns[1].WidthValue);
        Assert.Equal(100d, layout.Columns[2].WidthValue);
    }

    /// <summary>Eine breiter gezogene Spalte ist eine Handeinstellung und wird nie verkleinert.</summary>
    [Fact]
    public void Eine_breitere_Handeinstellung_bleibt_stehen()
    {
        var layout = Layout(Spalte(FieldKeys.Street, breite: 260d));

        ZahlenRechtsMigration.Entscheide(layout);

        Assert.Equal(260d, layout.Columns[0].WidthValue);
        Assert.Equal("Pixel", layout.Columns[0].WidthUnitType);
    }

    /// <summary>
    /// `SizeToHeader` ist keine Wahl des Benutzers, sondern die Kopfbreite — genau das, was
    /// der Neuaufbau heute durch die Startbreite ersetzt.
    /// </summary>
    [Fact]
    public void Eine_Kopfbreite_wird_durch_die_Startbreite_ersetzt()
    {
        var layout = Layout(Spalte(FieldKeys.HoldingName, breite: 1d, einheit: "SizeToHeader"));

        ZahlenRechtsMigration.Entscheide(layout);

        Assert.Equal(150d, layout.Columns[0].WidthValue);
        Assert.Equal("Pixel", layout.Columns[0].WidthUnitType);
    }

    [Fact]
    public void Spalten_ohne_Startbreite_behalten_ihre_Breite()
    {
        var layout = Layout(Spalte(FieldKeys.NominalDiameterMm, breite: 60d));

        ZahlenRechtsMigration.Entscheide(layout);

        Assert.Equal(60d, layout.Columns[0].WidthValue);
        Assert.Equal("Right", layout.Columns[0].HorizontalAlignment);
    }

    /// <summary>Schachtfelder heissen nach der Excel-Kopfzeile; mit Faltung greift dieselbe Regel.</summary>
    [Fact]
    public void Mit_Faltung_greift_die_Regel_auch_fuer_Schachtfelder()
    {
        var layout = Layout(
            Spalte("Dimension 1 mm"),
            Spalte("Schachtnummer", breite: 72d),
            Spalte("Material", breite: 72d));

        ZahlenRechtsMigration.Entscheide(layout, SchachtFeldnamen.Falte);

        Assert.Equal("Right", layout.Columns[0].HorizontalAlignment);
        Assert.Equal(150d, layout.Columns[1].WidthValue);
        Assert.Equal(100d, layout.Columns[2].WidthValue);
    }

    [Fact]
    public void Ein_leeres_Layout_setzt_nur_das_Flag()
    {
        var layout = new DataPageLayoutSettings();

        Assert.True(ZahlenRechtsMigration.Entscheide(layout));
        Assert.True(layout.ZahlenRechtsEinmalGesetzt);
        Assert.Empty(layout.Columns);
    }

    /// <summary>Gespeichert wird nur bei einer echten Aenderung — genau wie bei der Kompakt-Regel.</summary>
    [Fact]
    public void WendeAn_speichert_einmal_und_danach_nie_wieder()
    {
        var layout = Layout(Spalte(FieldKeys.NominalDiameterMm));
        var speicherungen = 0;

        ZahlenRechtsMigration.WendeAn(layout, () => speicherungen++);
        ZahlenRechtsMigration.WendeAn(layout, () => speicherungen++);

        Assert.Equal(1, speicherungen);
        Assert.Equal("Right", layout.Columns[0].HorizontalAlignment);
    }
}
