using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Bindet den Druckcenter-Bereich an zwei Umschalt-Knoepfe. Abhaken darf den Bereich
/// nicht veraendern — sonst wuerde der abgewaehlte Knopf den gerade gesetzten Wert
/// wieder ueberschreiben.
/// </summary>
public sealed class DruckcenterRowKindToBoolConverterTests
{
    private static readonly DruckcenterRowKindToBoolConverter Converter = new();

    [Fact]
    public void Convert_meldet_true_beim_passenden_Bereich()
    {
        var result = Converter.Convert(
            DruckcenterRowKind.Schacht, typeof(bool), "Schacht", CultureInfo.InvariantCulture);

        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_meldet_false_beim_anderen_Bereich()
    {
        var result = Converter.Convert(
            DruckcenterRowKind.Haltung, typeof(bool), "Schacht", CultureInfo.InvariantCulture);

        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_liefert_den_Bereich_beim_Anhaken()
    {
        var result = Converter.ConvertBack(
            true, typeof(DruckcenterRowKind), "Schacht", CultureInfo.InvariantCulture);

        Assert.Equal(DruckcenterRowKind.Schacht, result);
    }

    [Fact]
    public void ConvertBack_aendert_nichts_beim_Abhaken()
    {
        var result = Converter.ConvertBack(
            false, typeof(DruckcenterRowKind), "Schacht", CultureInfo.InvariantCulture);

        Assert.Same(Binding.DoNothing, result);
    }
}
