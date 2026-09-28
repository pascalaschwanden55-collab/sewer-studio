using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 9 («Export- und Import-Seite ordnen»): Waechter fuer die
/// Regel «hoechstens ein <c>ToolbarButtonAccent</c> je Abschnitt».
///
/// <para><b>Definition «Abschnitt»</b> (fuer diesen Waechter): ein Container-Element (in der
/// Praxis ein <c>Border</c>) mit einem <c>x:Name</c>, das mit dem Praefix <c>"Abschnitt"</c>
/// beginnt — z. B. <c>AbschnittExcelListen</c>, <c>AbschnittKatasterXtf</c>. ExportPage.xaml
/// traegt vier solche Container (Excel-Listen, Dateien verteilen, Kataster (XTF), WebGIS),
/// ImportPage.xaml drei (Normalfall, Einzelne Quellen, Nacharbeiten). Ein Abschnitt darf
/// beliebig viele Unter-Karten enthalten (z. B. die zwei XTF-Wege) — es zaehlt nur die Anzahl
/// echter Akzentknoepfe (<c>Style="{...Resource ToolbarButtonAccent}"</c>) irgendwo darin.</para>
/// </summary>
public sealed class ExportImportAbschnittsKnopfTests
{
    private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("ExportPage.xaml")]
    [InlineData("ImportPage.xaml")]
    public void Jeder_Abschnitt_hat_hoechstens_einen_Akzentknopf(string dateiname)
    {
        var xaml = Lies(dateiname);
        var verstoesse = FindeVerstoesse(xaml);

        Assert.True(
            verstoesse.Count == 0,
            $"{dateiname}: Abschnitte mit mehr als einem ToolbarButtonAccent-Knopf: "
            + string.Join(", ", verstoesse));
    }

    [Theory]
    [InlineData("ExportPage.xaml", 4)]
    [InlineData("ImportPage.xaml", 3)]
    public void Die_Seite_traegt_die_benannten_Abschnitte(string dateiname, int erwarteteAnzahl)
    {
        var abschnitte = Abschnitte(XDocument.Parse(Lies(dateiname)));
        Assert.Equal(erwarteteAnzahl, abschnitte.Count);
    }

    /// <summary>
    /// Beweist, dass die Regel wirklich prueft: ein kuenstlich zweiter Akzentknopf im selben
    /// Abschnitt wird erkannt (Sabotageprobe, kein vom Zufall gruener Test).
    /// </summary>
    [Fact]
    public void Sabotage_zwei_Akzentknoepfe_im_selben_Abschnitt_werden_erkannt()
    {
        const string sabotiert = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Border x:Name="AbschnittTest">
                        <StackPanel>
                            <Button Style="{StaticResource ToolbarButtonAccent}" Content="Eins"/>
                            <Button Style="{StaticResource ToolbarButtonAccent}" Content="Zwei"/>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </UserControl>
            """;

        var verstoesse = FindeVerstoesse(sabotiert);

        Assert.Single(verstoesse);
        Assert.Contains("AbschnittTest", verstoesse[0]);
    }

    /// <summary>Gegenprobe: genau ein Akzentknopf im Abschnitt bleibt erlaubt.</summary>
    [Fact]
    public void Ein_Akzentknopf_im_Abschnitt_ist_kein_Verstoss()
    {
        const string ok = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Border x:Name="AbschnittTest">
                        <StackPanel>
                            <Button Style="{StaticResource ToolbarButtonAccent}" Content="Eins"/>
                            <Button Style="{StaticResource ToolbarButton}" Content="Zwei"/>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </UserControl>
            """;

        Assert.Empty(FindeVerstoesse(ok));
    }

    private static string Lies(string dateiname)
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", dateiname));

    private static List<XElement> Abschnitte(XDocument doc)
        => doc.Descendants()
            .Where(e => ((string?)e.Attribute(XNs + "Name"))?.StartsWith("Abschnitt", System.StringComparison.Ordinal) == true)
            .ToList();

    private static List<string> FindeVerstoesse(string xaml)
    {
        var doc = XDocument.Parse(xaml);
        var ns = doc.Root!.Name.Namespace;
        var verstoesse = new List<string>();

        foreach (var abschnitt in Abschnitte(doc))
        {
            var anzahl = abschnitt.Descendants(ns + "Button").Count(IstAkzentknopf);
            if (anzahl > 1)
                verstoesse.Add($"{(string)abschnitt.Attribute(XNs + "Name")!} ({anzahl})");
        }

        return verstoesse;
    }

    private static bool IstAkzentknopf(XElement button)
    {
        var style = (string?)button.Attribute("Style");
        return style is not null && style.Contains("ToolbarButtonAccent", System.StringComparison.Ordinal);
    }
}
