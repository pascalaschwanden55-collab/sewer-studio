using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsPageLayoutTests
{
    [Fact]
    public void SettingsPage_strukturiert_einstellungen_in_arbeitsbereiche()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("<TabControl", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"TabStripPlacement\" Value=\"Left\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"Allgemein\"", xaml, StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "Allgemein",
                "Dateien und Ordner",
                "Import und Referenzdaten",
                "Video und KI",
                "Datensicherung",
                "Hilfe"
            },
            ReadTabHeaders(xaml));

        Assert.Contains("x:Key=\"SettingsNavigationMenuItem\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccentStrip\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"IsSelected\" Value=\"True\"", xaml, StringComparison.Ordinal);

        Assert.Contains("<TabItem Header=\"Dateien und Ordner\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"Import und Referenzdaten\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"Video und KI\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"Datensicherung\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<TabItem Header=\"Hilfe\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Projektdateien\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Datenordner und Logs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Programmbereinigung\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CleanProgramDataCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Pruefen und bereinigen\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Wiederherstellung\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Importquellen\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Referenzdaten\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"Werkzeuge\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Header=\"KI-Wissen\"", xaml, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(xaml, "Command=\"{Binding SaveCommand}\""));
        Assert.DoesNotContain("<TabItem Header=\"Projektpfade\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Programmdaten\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Importquellen\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Referenzdaten\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Video-Player\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"KI-Laufzeit\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Backup\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Pfade\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Datenordner\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Projekte &amp; Ordner\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Dateien &amp; Ordner\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Pfade und Daten\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Speicherorte\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Import &amp; Referenzdaten\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"Sicherung\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabItem Header=\"KI und Backup\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Projekt und Speicherorte\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Werkzeuge und Referenzdaten\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Programmordner\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Expander Header=\"Programm-Handbuch\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_haelt_kopfzeile_und_navigation_ausserhalb_des_scrollbereichs()
    {
        var xaml = ReadSettingsPage();

        var tabControlIndex = xaml.IndexOf("<TabControl", StringComparison.Ordinal);
        var firstScrollViewerIndex = xaml.IndexOf("<ScrollViewer", StringComparison.Ordinal);

        Assert.True(tabControlIndex >= 0, "SettingsPage braucht die linke Tab-Navigation.");
        Assert.True(firstScrollViewerIndex >= 0, "Die Tab-Inhalte muessen weiterhin scrollbar sein.");
        Assert.True(
            tabControlIndex < firstScrollViewerIndex,
            "Kopfzeile und linke Einstellungsnavigation duerfen nicht in einem aeusseren ScrollViewer liegen.");
        Assert.Contains("<TabControl Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsTabScrollViewer\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_navigation_zeigt_arbeitsbereiche_mit_kurzer_unterzeile()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("xmlns:settings=\"clr-namespace:AuswertungPro.Next.UI.Settings\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsMenuHeaderTitle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsMenuHeaderHint\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsMenuHeader\"", xaml, StringComparison.Ordinal);

        Assert.Equal(
            new[]
            {
                "Design, Diagnose, Speichern",
                "Projekte, Logs, Wiederherstellung",
                "Videos, PDF, XTF",
                "Wiedergabe, KI-Start, Schwellen",
                "KI-Wissen, PC-Schutz",
                "Handbuch, Kurzbefehle"
            },
            ReadMenuHeaderHints(xaml));
    }

    [Fact]
    public void SettingsPage_navigation_gruppiert_menuepunkte_in_lesbare_bereiche()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("x:Key=\"SettingsMenuSectionLabel\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("settings:SettingsNavigation.Group=\"\"", xaml, StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "Basis",
                "Daten",
                "Daten",
                "Betrieb",
                "Sicherung",
                "Hilfe"
            },
            ReadMenuGroups(xaml));
        Assert.Equal(
            new[]
            {
                "Basis",
                "Daten",
                "Betrieb",
                "Sicherung",
                "Hilfe"
            },
            ReadMenuSectionLabels(xaml));

        Assert.DoesNotContain("Path=ToolTip", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Trigger Property=\"ToolTip\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_navigation_trennt_menuegruppen_sichtbar()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("x:Name=\"GroupDivider\"", xaml, StringComparison.Ordinal);
        Assert.Contains("settings:SettingsNavigation.HasGroupDivider", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter TargetName=\"GroupDivider\" Property=\"Visibility\" Value=\"Visible\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter TargetName=\"GroupDivider\" Property=\"Margin\" Value=\"0,6,10,8\"/>", xaml, StringComparison.Ordinal);
        Assert.Equal(
            new[] { false, true, false, true, true, true },
            ReadMenuBooleanProperty(xaml, "HasGroupDivider"));
    }

    [Fact]
    public void SettingsPage_navigation_zeigt_gruppenlabel_nur_am_gruppenbeginn()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("settings:SettingsNavigation.IsGroupStart", xaml, StringComparison.Ordinal);
        Assert.Contains("Path=(settings:SettingsNavigation.IsGroupStart)", xaml, StringComparison.Ordinal);
        Assert.Contains("<Setter TargetName=\"SectionLabel\" Property=\"Visibility\" Value=\"Collapsed\"/>", xaml, StringComparison.Ordinal);
        Assert.Equal(
            new[] { true, true, false, true, true, true },
            ReadMenuBooleanProperty(xaml, "IsGroupStart"));
    }

    [Fact]
    public void SettingsPage_navigation_verwendet_kurze_arbeitsorientierte_unterzeilen()
    {
        var xaml = ReadSettingsPage();

        Assert.Equal(
            new[]
            {
                "Design, Diagnose, Speichern",
                "Projekte, Logs, Wiederherstellung",
                "Videos, PDF, XTF",
                "Wiedergabe, KI-Start, Schwellen",
                "KI-Wissen, PC-Schutz",
                "Handbuch, Kurzbefehle"
            },
            ReadMenuHeaderHints(xaml));

        Assert.DoesNotContain("Tag=\"Wiedergabe, Modelle\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("settings:SettingsNavigation.Hint=\"Wiedergabe, Modelle\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("settings:SettingsNavigation.Group=\"Ablage\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("settings:SettingsNavigation.Group=\"Datenquellen\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("settings:SettingsNavigation.Group=\"Sicherheit\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_navigation_zeigt_nummerierte_eintraege_zum_schnellen_scannen()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("x:Key=\"SettingsMenuIndexBadge\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Path=(settings:SettingsNavigation.Index)", xaml, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "01", "02", "03", "04", "05", "06" },
            ReadMenuIndexes(xaml));
    }

    [Fact]
    public void SettingsPage_navigation_hat_eigene_seitenleiste_und_inhaltsbereich()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("<ControlTemplate TargetType=\"{x:Type TabControl}\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"250\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NavigationPane\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Einstellungsbereiche\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentPane\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PART_SelectedContentHost\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsPage_formulare_verwenden_einheitliche_label_eingabe_aktion_zeilen()
    {
        var xaml = ReadSettingsPage();

        Assert.Contains("x:Key=\"SettingsFormGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsFieldLabel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsTextInput\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsBrowseButton\"", xaml, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(xaml, "Style=\"{StaticResource SettingsFieldLabel}\"") >= 12,
            "Die Einstellungen sollen konsistente Formularzeilen mit linker Label-Spalte verwenden.");
        Assert.DoesNotContain("<DockPanel>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"400\"", xaml, StringComparison.Ordinal);
    }

    private static string[] ReadTabHeaders(string xaml)
        => Regex.Matches(xaml, "<TabItem Header=\"([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static string[] ReadMenuHeaderHints(string xaml)
        => Regex.Matches(xaml, "settings:SettingsNavigation.Hint=\"([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static string[] ReadMenuSectionLabels(string xaml)
        => Regex.Matches(
                xaml,
                "settings:SettingsNavigation.Group=\"([^\"]+)\"",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToArray();

    private static string[] ReadMenuGroups(string xaml)
        => Regex.Matches(xaml, "settings:SettingsNavigation.Group=\"([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static string[] ReadMenuIndexes(string xaml)
        => Regex.Matches(xaml, "settings:SettingsNavigation.Index=\"([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static bool[] ReadMenuBooleanProperty(string xaml, string propertyName)
        => Regex.Matches(
                xaml,
                $"settings:SettingsNavigation.{propertyName}=\"(True|False)\"",
                RegexOptions.CultureInvariant)
            .Select(match => bool.Parse(match.Groups[1].Value))
            .ToArray();

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ReadSettingsPage()
        => File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SettingsPage.xaml"));
}
