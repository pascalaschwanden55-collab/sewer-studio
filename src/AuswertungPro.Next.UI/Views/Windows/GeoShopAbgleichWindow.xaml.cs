using System.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Nur Darstellung: Der Dialogdienst liefert Text und Freigabe.</summary>
public partial class GeoShopAbgleichWindow : Window
{
    public GeoShopAbgleichWindow() => InitializeComponent();

    public void Zeige(string bericht, bool darfUebernehmen)
    {
        Bericht.Text = bericht;
        Status.Text = darfUebernehmen ? "Prüfe die Änderungen. Erst mit Übernehmen werden sie ins Projekt geschrieben."
            : "Keine Übernahme möglich. Die Hinweise stehen unten.";
        Fortschritt.Visibility = Visibility.Collapsed;
        Uebernehmen.IsEnabled = darfUebernehmen;
    }

    private void Uebernehmen_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
