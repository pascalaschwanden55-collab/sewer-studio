using System.Windows;
using System.Linq;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Nur Darstellung: Der Dialogdienst liefert Text und Freigabe.</summary>
public partial class GeoShopAbgleichWindow : Window
{
    public GeoShopAbgleichWindow() => InitializeComponent();

    public void Zeige(GeoShopPlan plan)
    {
        Zeige(GeoShopAbgleichBericht.Schreibe(plan), plan.Positionen.Count > 0);
        var felder = plan.Positionen.SelectMany(p => p.Vergleich?.Felder ?? []).ToArray();
        Feldvergleich.ItemsSource = felder;
        Feldvergleich.Visibility = felder.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (felder.Length > 0)
            Status.Text = "Häkchen: GeoShop übernehmen. Ohne Häkchen: bisherigen Wert behalten. Handwerte bleiben geschützt.";
    }

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
