using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SettingsPage : UserControl
{
    private SettingsSearchController? _suche;

    public SettingsPage()
    {
        InitializeComponent();
    }

    /// <summary>Optikanalyse 28.09.2026, Aufgabe 6: Der Reiter «Hilfe» verweist nur noch auf das
    /// eigene, nicht-modale Handbuchfenster (dieselbe Einstiegsstelle wie F1/Hilfe-Menü).</summary>
    private void HandbuchOeffnen_Click(object sender, RoutedEventArgs e)
        => HandbuchWindow.ZeigeAn(owner: Window.GetWindow(this));

    /// <summary>Aufgabe 6: derselbe Weg wie Strg+F1/Hilfe-Menü.</summary>
    private void TastenkuerzelAnzeigen_Click(object sender, RoutedEventArgs e)
        => TastenkuerzelWindow.ZeigeAn(owner: Window.GetWindow(this));

    private void SucheBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Das Textfeld wird vor dem TabControl aufgebaut. Ein erstes XAML-Ereignis darf
        // deshalb waehrend InitializeComponent noch nichts filtern.
        if (EinstellungsReiter is null || SucheTreffer is null)
            return;

        _suche ??= new SettingsSearchController(EinstellungsReiter);
        var suche = SucheBox.Text;
        var sichtbar = _suche.Anwenden(suche);
        SucheTreffer.Text = string.IsNullOrWhiteSpace(suche)
            ? string.Empty
            : sichtbar == 0
                ? "keine Treffer"
                : sichtbar == 1
                    ? "1 Gruppe"
                    : $"{sichtbar} Gruppen";
    }
}
