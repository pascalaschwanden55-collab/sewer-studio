using System.Windows.Controls;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SettingsPage : UserControl
{
    private SettingsSearchController? _suche;

    public SettingsPage()
    {
        InitializeComponent();
    }

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
