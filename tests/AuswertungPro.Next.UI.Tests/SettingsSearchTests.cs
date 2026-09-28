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
        Assert.Contains("Text=\"Einstellung suchen…\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Optikanalyse 28.09.2026, Aufgabe 10a: Technische Gruppen liegen je Reiter zugeklappt
    /// unter «Erweitert (für Fachleute)». Ein Treffer darin darf nicht unsichtbar bleiben —
    /// der Controller muss den Expander bei einem Treffer aufklappen und ohne Suche wieder
    /// auf den Standardzustand (zugeklappt) zurueckstellen.
    /// </summary>
    [Fact]
    public void Controller_klappt_einen_Erweitert_Bereich_bei_Treffer_darin_auf_und_wieder_zu()
    {
        StaTestRunner.Run(() =>
        {
            var reiter = new TabControl();

            var sichtbareGruppe = new GroupBox
            {
                Header = "Werkzeuge",
                Content = new TextBlock { Text = "Telefonsuche" }
            };
            var technischeGruppe = new GroupBox
            {
                Header = "Werkzeuge (Fachleute)",
                Content = new TextBlock { Text = "pdftotext.exe auswaehlen" }
            };
            var erweitert = new Expander
            {
                Header = "Erweitert (für Fachleute)",
                IsExpanded = false,
                Content = technischeGruppe
            };
            var tab = new TabItem
            {
                Header = "Import und Referenzdaten",
                Content = new StackPanel { Children = { sichtbareGruppe, erweitert } }
            };
            reiter.Items.Add(tab);
            reiter.SelectedIndex = 0;

            var controller = new SettingsSearchController(reiter);

            // Ein Treffer nur in der zugeklappten Gruppe klappt sie auf.
            controller.Anwenden("pdftotext");
            Assert.Equal(System.Windows.Visibility.Collapsed, sichtbareGruppe.Visibility);
            Assert.Equal(System.Windows.Visibility.Visible, technischeGruppe.Visibility);
            Assert.True(erweitert.IsExpanded);

            // Ohne Suche geht der Bereich zurueck in seinen Standardzustand (zugeklappt).
            controller.Anwenden("");
            Assert.False(erweitert.IsExpanded);

            // Ein Treffer ohne Bezug zum Erweitert-Bereich klappt ihn nicht auf.
            controller.Anwenden("telefonsuche");
            Assert.False(erweitert.IsExpanded);
        });
    }
}
