using System.IO;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Settings;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Einstellungssuche ist Umlaut-tolerant, verknuepft mehrere Woerter mit UND
/// und liest alle sichtbaren Texte einer Gruppe.
/// </summary>
public sealed class SettingsSearchTests
{
    [Theory]
    [InlineData("Prüfen und bereinigen", "pruef", true)]
    [InlineData("Prüfen und bereinigen", "prüf", true)]
    [InlineData("Datenordner und Logs", "log ordner", true)]
    [InlineData("Datenordner und Logs", "log video", false)]
    [InlineData("KI-Schwellwerte", "schwell", true)]
    [InlineData("Video-Player", "", true)]
    public void Matcher_ist_umlaut_tolerant_und_verknuepft_Woerter_mit_UND(
        string text,
        string suche,
        bool erwartet)
        => Assert.Equal(erwartet, SettingsSearchMatcher.Passt(suche, [text]));

    [Fact]
    public void Matcher_liest_alle_Texte_einer_Gruppe_gemeinsam()
        => Assert.True(SettingsSearchMatcher.Passt(
            "fotos seite",
            ["Haltungsprotokoll (PDF)", "Fotos je Seite", "Gilt für selbst erzeugte Protokolle"]));

    [Fact]
    public void Controller_blendet_Gruppen_ohne_Treffer_aus_und_waehlt_den_ersten_Reiter_mit_Treffer()
    {
        StaTestRunner.Run(() =>
        {
            var reiter = new TabControl();
            var allgemein = new TabItem { Header = "Allgemein", Content = new StackPanel() };
            var videoGruppe = new GroupBox
            {
                Header = "Video-Player",
                Content = new TextBlock { Text = "Sprungweite in Sekunden" }
            };
            var kiGruppe = new GroupBox
            {
                Header = "KI-Schwellwerte",
                Content = new CheckBox { Content = "Mindest-Konfidenz für YOLO" }
            };
            var video = new TabItem
            {
                Header = "Video und KI",
                Content = new StackPanel { Children = { videoGruppe, kiGruppe } }
            };
            ((StackPanel)allgemein.Content).Children.Add(
                new GroupBox { Header = "Speichern", Content = new TextBlock { Text = "Autosave" } });
            reiter.Items.Add(allgemein);
            reiter.Items.Add(video);
            reiter.SelectedIndex = 0;

            var controller = new SettingsSearchController(reiter);

            Assert.Equal(1, controller.Anwenden("yolo"));
            Assert.Equal(System.Windows.Visibility.Collapsed, videoGruppe.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, kiGruppe.Visibility);
            Assert.Same(video, reiter.SelectedItem);

            Assert.Equal(3, controller.Anwenden(""));
            Assert.Equal(System.Windows.Visibility.Visible, videoGruppe.Visibility);
        });
    }

    [Fact]
    public void Die_Einstellungsseite_hat_ein_Suchfeld_im_Kopf()
    {
        var xaml = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));

        Assert.Contains("x:Name=\"SucheBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextChanged=\"SucheBox_TextChanged\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SucheTreffer\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "ToolTip=\"Einstellung suchen — zeigt nur passende Gruppen und springt zum ersten Reiter mit Treffer.\"",
            xaml,
            StringComparison.Ordinal);
    }
}
