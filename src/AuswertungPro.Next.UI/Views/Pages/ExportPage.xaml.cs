using System.Windows;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class ExportPage : System.Windows.Controls.UserControl
{
    public ExportPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        ButtonContextMenuOpener.OpenFromButton(sender, DataContext);
    }

    // Das Fenster «Verteilen» verweist auf die Ordnerbausteine; die Seite bringt sie ins Bild.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ExportPageViewModel alt)
            alt.VerteilEinstellungenAngefordert -= OnVerteilEinstellungenAngefordert;
        if (e.NewValue is ExportPageViewModel neu)
            neu.VerteilEinstellungenAngefordert += OnVerteilEinstellungenAngefordert;
    }

    private void OnVerteilEinstellungenAngefordert(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(() => VerzeichnisbaumTitel.BringIntoView());
}
