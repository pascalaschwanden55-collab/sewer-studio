using System;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Die Farben im Excel-Bericht tragen Bedeutung. Der C#-Vertrag und der
/// Python-Vorlagenbauer werden deshalb durch einen Vorlagentreuetest gegeneinander
/// geprueft; eine stille Abweichung darf nicht mehr bis zur fertigen Datei gelangen.
///
/// Vorher lagen sie in zwei binaeren Vorlagendateien und waren auseinandergelaufen:
/// Zustandsklasse 3 war bei den Haltungen AEB135, bei den Schaechten A5A832.
/// </summary>
public sealed class ExcelReportStyleTests
{
    [Theory]
    [InlineData("0", "FFFF0000")] // rot
    [InlineData("1", "FFFF6600")] // orange
    [InlineData("2", "FFFFFF00")] // gelb
    [InlineData("3", "FFAEB135")] // oliv
    [InlineData("4", "FF92D050")] // gruen
    public void Zustandsklasse_hat_ihre_festgelegte_farbe(string klasse, string erwartet)
    {
        var regel = ExcelReportStyle.Zustandsklassen.Single(r => r.Wert == klasse);

        Assert.Equal(erwartet, regel.Farbe);
    }

    [Fact]
    public void Zustandsklassen_decken_null_bis_vier_ab()
    {
        Assert.Equal(
            new[] { "0", "1", "2", "3", "4" },
            ExcelReportStyle.Zustandsklassen.Select(r => r.Wert).ToArray());
    }

    [Fact]
    public void Jede_zustandsklasse_hat_eine_eigene_farbe()
    {
        var farben = ExcelReportStyle.Zustandsklassen.Select(r => r.Farbe).ToList();

        Assert.Equal(farben.Count, farben.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("Bund", "FFFF8000")]
    [InlineData("Gemeinde", "FF00B0F0")]
    [InlineData("Privat", "FFFF0000")]
    [InlineData("Kanton", "FFFFFF00")]
    public void Eigentuemer_behalten_ihre_farbe(string eigentuemer, string erwartet)
    {
        var regel = ExcelReportStyle.Eigentuemer.Single(r =>
            r.Wert.Equals(eigentuemer, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(erwartet, regel.Farbe);
    }

    [Fact]
    public void Gruentoene_mit_verschiedener_bedeutung_bleiben_getrennt()
    {
        // Bewusst KEIN Vereinheitlichen: 92D050 = guter Zustand / bestandene
        // Pruefung, 548235 = AWU, 00B050 = Arbeit abgeschlossen.
        Assert.Equal("FF92D050", ExcelReportStyle.Zustandsklassen.Single(r => r.Wert == "4").Farbe);
        Assert.Equal("FF548235", ExcelReportStyle.Eigentuemer.Single(r => r.Wert == "AWU").Farbe);
        Assert.Equal("FF00B050", ExcelReportStyle.Status.Single(r => r.Wert == "abgeschlossen").Farbe);
    }

    [Theory]
    [InlineData("i.O.", "FF92D050")]
    [InlineData("beobachten", "FFFFFF00")]
    [InlineData("Sanierungsbedarf", "FFFF0000")]
    [InlineData("Prüfung bestanden", "FF92D050")]
    [InlineData("Prüfung knapp nicht bestanden", "FFFFFF00")]
    [InlineData("Prüfung nicht bestanden (grob undicht)", "FFFF0000")]
    [InlineData("Pruefung bestanden", "FF92D050")]
    [InlineData("Pruefung knapp nicht bestanden", "FFFFFF00")]
    [InlineData("Pruefung nicht bestanden (grob undicht)", "FFFF0000")]
    [InlineData("Keine", "FFE7E6E6")]
    public void Pruefungsresultat_deckt_beide_Wertefamilien_ab(
        string pruefungsresultat,
        string erwarteteFarbe)
    {
        var regel = ExcelReportStyle.Pruefungsresultate.Single(r =>
            r.Wert.Equals(pruefungsresultat, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(erwarteteFarbe, regel.Farbe);
    }

    [Fact]
    public void Pruefungsresultat_verwendet_fuer_beide_Familien_dieselbe_Ampel()
    {
        Assert.Equal(
            FarbeFuer("i.O."),
            FarbeFuer("Prüfung bestanden"));
        Assert.Equal(
            FarbeFuer("beobachten"),
            FarbeFuer("Prüfung knapp nicht bestanden"));
        Assert.Equal(
            FarbeFuer("Sanierungsbedarf"),
            FarbeFuer("Prüfung nicht bestanden (grob undicht)"));
    }

    [Fact]
    public void Status_trennt_offen_von_abgeschlossen()
    {
        Assert.Equal(
            "FFFF0000",
            ExcelReportStyle.Status.Single(r => r.Wert == "offen").Farbe);
        Assert.Equal(
            "FF00B050",
            ExcelReportStyle.Status.Single(r => r.Wert == "abgeschlossen").Farbe);
    }

    [Fact]
    public void Alle_farben_sind_vollstaendige_argb_werte()
    {
        // ClosedXML/Excel erwartet AARRGGBB. Ein verkuerzter Wert faellt sonst erst
        // in der fertigen Datei auf.
        var alle = ExcelReportStyle.Zustandsklassen
            .Concat(ExcelReportStyle.Eigentuemer)
            .Concat(ExcelReportStyle.Pruefungsresultate)
            .Concat(ExcelReportStyle.Status)
            .Select(r => r.Farbe);

        foreach (var farbe in alle)
        {
            Assert.Equal(8, farbe.Length);
            Assert.True(
                farbe.All(Uri.IsHexDigit),
                $"Farbe '{farbe}' ist kein Hexwert.");
        }
    }

    private static string FarbeFuer(string wert)
        => ExcelReportStyle.Pruefungsresultate.Single(r =>
            r.Wert.Equals(wert, StringComparison.OrdinalIgnoreCase)).Farbe;
}
