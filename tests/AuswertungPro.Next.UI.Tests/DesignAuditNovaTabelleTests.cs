using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 1 (Inventar 4.3): Tabellenkopf in Grossbuchstaben, Zahlenspalten
/// rechtsbuendig, NR nur in "Alle Spalten". Reiner Text-Waechter wie
/// <see cref="DesignAuditNovaPaletteTests"/>; laedt keine echten WPF-Ressourcen.
/// </summary>
public sealed class DesignAuditNovaTabelleTests
{
    /// <summary>Der ganze DataGridColumnHeader-Stil eines Themes als Text.</summary>
    private static string Kopfstil(string datei)
    {
        var xaml = DesignAuditNovaPaletteTests.Xaml(datei);
        var header = Regex.Match(xaml, "<Style TargetType=\"\\{x:Type DataGridColumnHeader\\}\">[\\s\\S]*?\n    </Style>").Value;
        Assert.True(header.Length > 0, $"{datei}: DataGridColumnHeader-Stil nicht gefunden");
        return header;
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Tabellenkopf_schreibt_gross(string datei)
    {
        var header = Kopfstil(datei);

        // Kapitaelchen greifen mit der Programmschrift nicht (Prototyp: echte Grossbuchstaben,
        // Letter-Spacing). Die alte Regel darf nicht mehr im Kopf-Template stehen.
        Assert.DoesNotContain("Typography.Capitals", header);

        // Nur String-Koepfe bekommen die typografische Behandlung: eine typgebundene DataTemplate
        // fuer sys:String. Die Grossschreibung selbst passiert NICHT hier per Konverter, sondern
        // beim Erzeugen der Spalte (DataPageColumnFactoryTests,
        // GrossbuchstabenConverterTests): Theme.xaml/ThemeLight.xaml werden von
        // PageTitleUnderlineTests roh per XamlReader.Load geladen, wo ein eigener Klassenverweis
        // (auch nur als Ressource, egal wie tief verschachtelt) mit "unbekannter Typ" scheitert —
        // dieser Waechter haelt das bewusst fest. Ein nicht-textueller Kopf durchlaeuft diese
        // Vorlage nicht und behaelt den bisherigen ContentPresenter ohne ContentTemplate.
        Assert.Contains("DataType=\"{x:Type sys:String}\"", header);
        Assert.Contains("Text=\"{Binding}\"", header);
        Assert.DoesNotContain("controls:", header);
        Assert.DoesNotContain("Converter=", header);
        Assert.Contains("<ContentPresenter VerticalAlignment=\"Center\"/>", header);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", header);

        // Nova-Fixwelle 2b (F4): Groesse und Tinte erbt die Vorlage vom Kopf. Vorher standen
        // sie fest im Template und gewannen gegen jeden Setter — auch gegen einen abgeleiteten
        // ColumnHeaderStyle, der eine Spalte einfaerben will.
        Assert.Contains(
            "FontSize=\"{Binding FontSize, RelativeSource={RelativeSource AncestorType={x:Type DataGridColumnHeader}}}\"",
            header);
        Assert.Contains(
            "Foreground=\"{Binding Foreground, RelativeSource={RelativeSource AncestorType={x:Type DataGridColumnHeader}}}\"",
            header);
        // Damit die Vererbung ueberhaupt etwas liefert, muss der Kopfstil selbst Groesse und
        // Tinte setzen — die Groesse als Token, nicht als nackte Zahl.
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"{DynamicResource TextXS}\"/>", header);
        Assert.Contains("<Setter Property=\"Foreground\" Value=\"{DynamicResource MutedBrush}\"/>", header);
    }

    /// <summary>
    /// Nova-Fixwelle 2b (P4): Die Ziehgriffe zwischen den Kopfzellen tragen eine eigene
    /// Vorlage. Ohne sie zeichnete WPF seine Standardoptik — im dunklen Theme fast weiss und
    /// damit auffaelliger als der Kopftext. Nur der rechte Griff zeigt eine Linie, sonst
    /// staende an jeder Spaltengrenze eine doppelte.
    /// </summary>
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Die_Kopfgriffe_zeichnen_hoechstens_eine_dezente_Trennlinie(string datei)
    {
        var header = Kopfstil(datei);

        Assert.Contains("x:Key=\"KopfGriffOhneLinie\"", header);
        Assert.Contains("x:Key=\"KopfGriffTrennlinie\"", header);
        Assert.Contains("PART_LeftHeaderGripper\" Style=\"{StaticResource KopfGriffOhneLinie}\"", header);
        Assert.Contains("PART_RightHeaderGripper\" Style=\"{StaticResource KopfGriffTrennlinie}\"", header);
        Assert.Contains("Fill=\"{DynamicResource BorderBrush}\"", header);

        // Der Ziehbereich bleibt: 6 px breit, Cursor SizeWE.
        Assert.Equal(2, Regex.Matches(header, "Width=\"6\" Cursor=\"SizeWE\"").Count);
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 3: Masse der Zustandsklassen-Marke und die einzeilige Zeilenhoehe
    /// stehen als Tokens in Controls.xaml, nicht als Zahl in der Spaltenfabrik.
    /// </summary>
    [Fact]
    public void Chip_Masse_und_Zeilenhoehe_liegen_als_Tokens_in_Controls_xaml()
    {
        var controls = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Controls.xaml"));

        Assert.Contains("<sys:Double x:Key=\"ZustandsklasseChipBreite\">34</sys:Double>", controls);
        Assert.Contains("<sys:Double x:Key=\"ZustandsklasseChipHoehe\">22</sys:Double>", controls);
        Assert.Contains("<sys:Double x:Key=\"RowHeightCompact\">34</sys:Double>", controls);
    }

    /// <summary>
    /// Die beiden neuen Spaltenfabriken werden im Code gebaut. Farben und Masse duerfen dort
    /// nur ueber <c>SetResourceReference</c> aus dem Theme kommen — sonst bliebe ein
    /// Themenwechsel zur Laufzeit wirkungslos, und eine feste Farbe waere im dunklen Theme
    /// unlesbar.
    /// </summary>
    [Theory]
    [InlineData("ZustandsklasseChipColumnFactory.cs")]
    [InlineData("HaltungStatusColumnFactory.cs")]
    public void Die_Spaltenfabriken_lesen_Farben_nur_aus_dem_Theme(string datei)
    {
        var quelle = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));

        var festeFarbe = Regex.Matches(quelle, "#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})");
        Assert.True(festeFarbe.Count == 0, $"{datei}: feste Farbwerte {string.Join(", ", festeFarbe.Select(m => m.Value))}");
        Assert.DoesNotContain("Color.FromRgb", quelle);
        Assert.Contains("SetResourceReference", quelle);
    }

    /// <summary>
    /// Die Marke traegt ihre Masse aus den Tokens und ihre Tinte aus der Kontrastregel; die
    /// Klassenfarbe selbst bleibt die unveraenderte Palette.
    /// </summary>
    [Fact]
    public void Die_Zustandsklassen_Marke_verwendet_Tokens_Palette_und_Kontrastregel()
    {
        var quelle = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ZustandsklasseChipColumnFactory.cs"));

        Assert.Contains("\"ZustandsklasseChipBreite\"", quelle);
        Assert.Contains("\"ZustandsklasseChipHoehe\"", quelle);
        Assert.Contains("\"RadiusS\"", quelle);
        Assert.Contains("\"FontMono\"", quelle);
        Assert.Contains("ZustandsklasseInkConverter", quelle);
        Assert.Contains("ZustandsklasseColorPalette", quelle);
    }
}
