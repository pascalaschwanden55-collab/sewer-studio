using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditContrastTests
{
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Auswahl_bleibt_lesbar_und_ihre_Kontur_erkennbar(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        Assert.True(Contrast(ReadColor(xaml, "ColorSelectionText"), ReadColor(xaml, "ColorSelection")) >= 4.5);
        Assert.True(Contrast(ReadColor(xaml, "ColorSelectionBorder"), ReadColor(xaml, "ColorSelection")) >= 3);
        Assert.True(Contrast(ReadColor(xaml, "ColorSelectionBorder"), ReadColor(xaml, "ColorCard")) >= 3);
    }

    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Primary_and_success_buttons_reach_normal_text_contrast(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.True(Contrast("#FFFFFFFF", ReadColor(xaml, "ColorAccent")) >= 4.5);
        Assert.True(Contrast("#FFFFFFFF", ReadColor(xaml, "ColorAccentHover")) >= 4.5);
        Assert.True(Contrast("#FFFFFFFF", ReadColor(xaml, "ColorSuccess")) >= 4.5);
    }

    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Ki_text_reaches_normal_text_contrast_on_card_and_ki_subtle(string themeFile)
    {
        // Nova-Etappe 1: KI-Farbe getrennt vom Akzent, lesbar auf Karte und auf der KI-Flaeche.
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        Assert.True(Contrast(ReadColor(xaml, "ColorKiText"), ReadColor(xaml, "ColorCard")) >= 4.5);
        Assert.True(Contrast(ReadColor(xaml, "ColorKiText"), ReadColor(xaml, "ColorKiSubtle")) >= 4.5);
        Assert.Contains("x:Key=\"KiBrush\"", xaml);
        Assert.Contains("x:Key=\"KiSubtleBrush\"", xaml);
        Assert.Contains("x:Key=\"KiTextBrush\"", xaml);
    }

    /// <summary>
    /// Nova-Fixwelle F7: Der Kartenrand bleibt bewusst hell (Glas-Look). Eingabefelder und
    /// Knopf-Umrisse brauchen dagegen eine erkennbare Kontur — mindestens 3:1 gegen die
    /// Kartenflaeche (WCAG 1.4.11, Bedienelement-Umriss).
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Eingabekontur_hebt_sich_von_der_Karte_ab(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        Assert.Contains("x:Key=\"InputBorderBrush\"", xaml);
        Assert.True(
            Contrast(ReadColor(xaml, "ColorInputBorder"), ReadColor(xaml, "ColorCard")) >= 3,
            $"{themeFile}: InputBorderBrush erreicht auf CardBrush keine 3:1.");
    }

    /// <summary>
    /// Nova-Fixwelle 2b (P4): Die Trennlinie zwischen den Kopfzellen ist Beiwerk. Sie darf sich
    /// deshalb nicht staerker vom Kopfgrund abheben als der Kopftext selbst — genau das war im
    /// dunklen Theme der Fehler (Standardgriff von WPF, fast weiss auf dunkelblauem Kopf).
    ///
    /// Die Regel ist bewusst als Kontrast formuliert und nicht als "nicht heller": Im hellen
    /// Theme ist eine Linie HELLER als die Tinte gerade das Unauffaellige, im dunklen das
    /// Auffaellige. Der Vergleich gegen den Kopfgrund gilt in beiden Themes gleich.
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Die_Kopf_Trennlinie_draengt_sich_nicht_vor_den_Kopftext(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        var grund = ReadColor(xaml, "ColorHeader");
        var linie = Contrast(ReadColor(xaml, "ColorBorder"), grund);
        var tinte = Contrast(ReadColor(xaml, "ColorTextMuted"), grund);

        Assert.True(
            linie < tinte,
            $"{themeFile}: Kopf-Trennlinie {linie:0.00}:1 gegen den Kopfgrund, Kopftext nur {tinte:0.00}:1.");
    }

    [Fact]
    public void Muted_dark_text_and_light_warning_text_reach_normal_text_contrast()
    {
        var dark = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "Theme.xaml"));
        var light = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", "ThemeLight.xaml"));

        Assert.True(Contrast(ReadColor(dark, "ColorTextMuted"), ReadColor(dark, "ColorCard")) >= 4.5);
        Assert.True(Contrast(ReadColor(light, "ColorWarning"), ReadColor(light, "ColorCard")) >= 4.5);
    }

    /// <summary>
    /// Aufgabe 4b, Fix-Runde 1: das neue <c>WarningButton</c> (Controls.xaml) traegt
    /// <c>WarningTextBrush</c> als Vordergrund auf der Kartenflaeche (BasedOn SecondaryButton,
    /// wie das bestehende <c>DangerButton</c> mit <c>DangerTextBrush</c>). Beide Paarungen waren
    /// bisher nicht automatisiert geprueft - dieser Test haelt die schon beim Anlegen von
    /// WarningButton nachgerechneten Werte fest (6,28:1 / 5,02:1 Warning, 6,29:1 / 4,83:1 Danger).
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Warning_and_danger_button_text_reach_normal_text_contrast_on_card(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.True(
            Contrast(WarningTextColor(xaml), ReadColor(xaml, "ColorCard")) >= 4.5,
            $"{themeFile}: WarningTextBrush erreicht auf CardBrush keine 4,5:1.");
        Assert.True(
            Contrast(DangerTextColor(xaml), ReadColor(xaml, "ColorCard")) >= 4.5,
            $"{themeFile}: DangerTextBrush erreicht auf CardBrush keine 4,5:1.");
    }

    /// <summary>
    /// Fix-Runde 1 (Review 29.09.2026, Aufgabe 12): Vier neu eingefuehrte Text-/Hintergrund-
    /// Paarungen, jede in beiden Themes. SuccessTextBrush auf CardBrush fehlte bisher als
    /// eigener Test (nur Warning/Danger waren geprueft) und wird jetzt fuer
    /// CodingSessionViewModel.GetConfidenceBrush und HydraulikPanelWindow.AuslastungRun gebraucht.
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Success_button_text_reaches_normal_text_contrast_on_card(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.True(
            Contrast(ReadSolidColorBrush(xaml, "SuccessTextBrush"), ReadColor(xaml, "ColorCard")) >= 4.5,
            $"{themeFile}: SuccessTextBrush erreicht auf CardBrush keine 4,5:1.");
    }

    /// <summary>
    /// TrainingStudioWindow.QualityWarning stand auf WarningBrush mit weisser Schrift (2,52:1 im
    /// Dunkelmodus). Die Kombination WarningTextBrush auf WarningSubtleBrush ist die schon
    /// etablierte "farbige Subtle-Flaeche + *TextBrush"-Badge-Form.
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Warning_text_reaches_normal_text_contrast_on_warning_subtle(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.True(
            Contrast(ReadSolidColorBrush(xaml, "WarningTextBrush"), ReadSolidColorBrush(xaml, "WarningSubtleBrush")) >= 4.5,
            $"{themeFile}: WarningTextBrush erreicht auf WarningSubtleBrush keine 4,5:1.");
    }

    /// <summary>
    /// HydraulikPanelWindow.AblagerungVerdictText sitzt auf SuccessSubtleBrush/DangerSubtleBrush.
    /// SuccessTextBrush/DangerTextBrush waeren hier NICHT durchgehend sicher — DangerTextBrush
    /// erreicht auf DangerSubtleBrush im Hellmodus nur 3,95:1. Die normale TextBrush (die
    /// Standard-Schriftfarbe) erreicht auf beiden Subtle-Flaechen in beiden Themes komfortabel
    /// ueber 9:1, weil die Subtle-Flaechen wie CardBrush je Theme hell bzw. dunkel sind.
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Text_brush_reaches_normal_text_contrast_on_success_and_danger_subtle(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        var text = ReadColor(xaml, "ColorTextPrimary");

        Assert.True(
            Contrast(text, ReadSolidColorBrush(xaml, "SuccessSubtleBrush")) >= 4.5,
            $"{themeFile}: TextBrush erreicht auf SuccessSubtleBrush keine 4,5:1.");
        Assert.True(
            Contrast(text, ReadSolidColorBrush(xaml, "DangerSubtleBrush")) >= 4.5,
            $"{themeFile}: TextBrush erreicht auf DangerSubtleBrush keine 4,5:1.");
    }

    /// <summary>
    /// SanierungsmassnahmenWindow-Zaehler-Abzeichen: weder "White" (3,35:1/2,52:1 dunkel) noch
    /// StatusBadgeTextBrush (3,97:1/3,82:1 hell — der Token ist fuer die theme-gleichen
    /// Zustandsklassen-Abzeichen Z0-Z4 gedacht, nicht fuer DangerBrush/WarningBrush) erreichen
    /// 4,5:1 in beiden Themes. DangerBadgeTextBrush/WarningBadgeTextBrush sind dafuer eigens
    /// verifiziert (dunkel: nahezu schwarz, hell: weiss — je nachdem wie hell die Flaeche im
    /// jeweiligen Theme ist).
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Badge_text_reaches_normal_text_contrast_on_danger_and_warning_fill(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));

        Assert.True(
            Contrast(ReadSolidColorBrush(xaml, "DangerBadgeTextBrush"), ReadColor(xaml, "ColorDanger")) >= 4.5,
            $"{themeFile}: DangerBadgeTextBrush erreicht auf DangerBrush keine 4,5:1.");
        Assert.True(
            Contrast(ReadSolidColorBrush(xaml, "WarningBadgeTextBrush"), ReadColor(xaml, "ColorWarning")) >= 4.5,
            $"{themeFile}: WarningBadgeTextBrush erreicht auf WarningBrush keine 4,5:1.");
    }

    private static string WarningTextColor(string xaml) => ReadSolidColorBrush(xaml, "WarningTextBrush");

    private static string DangerTextColor(string xaml) => ReadSolidColorBrush(xaml, "DangerTextBrush");

    private static string ReadSolidColorBrush(string xaml, string key)
    {
        var match = Regex.Match(
            xaml,
            $"<SolidColorBrush\\s+x:Key=\"{Regex.Escape(key)}\"\\s+Color=\"(?<value>#[0-9A-Fa-f]{{8}})\"");
        Assert.True(match.Success, $"SolidColorBrush {key} fehlt.");
        return match.Groups["value"].Value;
    }

    [Theory]
    [InlineData("Views/Windows/BeobachtungenWindow.xaml")]
    [InlineData("Views/ProtocolObservationsWindow.xaml")]
    [InlineData("Views/ProtocolCodePickerDialog.xaml")]
    [InlineData("Views/Windows/MediaSearchWindow.xaml")]
    public void Standard_dialogs_do_not_keep_the_removed_light_only_colors(string relativePath)
    {
        var parts = new[] { "src", "AuswertungPro.Next.UI" }
            .Concat(relativePath.Split('/'))
            .ToArray();
        var xaml = File.ReadAllText(RepoFile(parts));
        var removedColors = new[]
        {
            "#F8FAFC", "#D7DEE8", "#EEF5FB", "#E5EAF1", "#FBFDFF",
            "#FFF0F2F5", "#FFD0D7E2", "#FFFFF3E0", "#FFFFCC80",
            "#E6F9F0", "#FFF7E6", "#FEF0F0", "#F8F8F8", "#CCCCCC",
        };

        foreach (var color in removedColors)
            Assert.DoesNotContain(color, xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadColor(string xaml, string key)
    {
        var match = Regex.Match(
            xaml,
            $"<Color\\s+x:Key=\"{Regex.Escape(key)}\">(?<value>#[0-9A-Fa-f]{{8}})</Color>");
        Assert.True(match.Success, $"Theme-Farbe {key} fehlt.");
        return match.Groups["value"].Value;
    }

    private static double Contrast(string first, string second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05)
               / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double Luminance(string argb)
    {
        var rgb = argb.Length == 9 ? argb[3..] : argb[1..];
        var red = Channel(rgb[0..2]);
        var green = Channel(rgb[2..4]);
        var blue = Channel(rgb[4..6]);
        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double Channel(string hex)
    {
        var value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
