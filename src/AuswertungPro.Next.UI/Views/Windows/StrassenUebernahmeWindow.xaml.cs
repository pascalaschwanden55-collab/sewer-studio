using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Eine Zeile der Vorschau. <see cref="Uebernehmen"/> ist die Entscheidung des
/// Bearbeiters; vorbelegt ist sie mit ja, weil jede Zeile hier bereits
/// eindeutig ist.
/// </summary>
public sealed class StrassenUebernahmeAuswahl
{
    public StrassenUebernahmeAuswahl(StrassenUebernahmeZeile zeile)
    {
        ArgumentNullException.ThrowIfNull(zeile);
        Nummer = zeile.Nummer;
        Wert = zeile.Wert;
        Herkunft = zeile.Herkunft;
    }

    public string Nummer { get; }
    public string Wert { get; }
    public string Herkunft { get; }
    public bool Uebernehmen { get; set; } = true;
}

/// <summary>
/// Zeigt alle uebertragbaren Strassen auf einmal und schreibt erst nach
/// Bestaetigung. Ein Stapellauf ohne Vorschau waere hier falsch: Die Quelle
/// ist das eigene Projekt, und ein Fehler darin vervielfaeltigte sich sonst
/// still ueber die ganze Liste.
///
/// Widerspruechliche Faelle stehen nicht in der Liste, werden aber unten
/// benannt — sie gehoeren einzeln entschieden, nicht im Stapel.
/// </summary>
public partial class StrassenUebernahmeWindow : Window
{
    private readonly ObservableCollection<StrassenUebernahmeAuswahl> _zeilen;

    public StrassenUebernahmeWindow(
        string titel,
        string spaltenkopf,
        IReadOnlyList<StrassenUebernahmeZeile> vorschlaege,
        IReadOnlyList<string> offeneFaelle)
    {
        ArgumentNullException.ThrowIfNull(vorschlaege);
        ArgumentNullException.ThrowIfNull(offeneFaelle);

        InitializeComponent();

        Title = titel;
        NummerSpalte.Header = spaltenkopf;
        _zeilen = new ObservableCollection<StrassenUebernahmeAuswahl>(
            vorschlaege.Select(v => new StrassenUebernahmeAuswahl(v)));
        Liste.ItemsSource = _zeilen;

        KopfText.Text = _zeilen.Count switch
        {
            0 => "Es gibt nichts zu übernehmen: Entweder sind die Felder bereits gefüllt, "
                 + "oder die Nachbarn führen selbst keine Strasse.",
            1 => "1 leeres Feld kann die Strasse seines Nachbarn übernehmen.",
            _ => $"{_zeilen.Count} leere Felder können die Strasse ihres Nachbarn übernehmen."
        };

        UebernehmenKnopf.IsEnabled = _zeilen.Count > 0;
        AlleKnopf.IsEnabled = _zeilen.Count > 0;
        KeineKnopf.IsEnabled = _zeilen.Count > 0;

        if (offeneFaelle.Count > 0)
        {
            OffeneText.Visibility = Visibility.Visible;
            OffeneText.Text =
                $"Nicht im Stapel: {string.Join(", ", offeneFaelle)} — dort widersprechen sich "
                + "die angrenzenden Strassen. Bitte einzeln über den Rechtsklick entscheiden.";
        }
    }

    /// <summary>Nur die angehakten Zeilen. Leer, wenn abgebrochen wurde.</summary>
    public IReadOnlyList<StrassenUebernahmeAuswahl> Gewaehlt { get; private set; } = [];

    private void Alle_Click(object sender, RoutedEventArgs e) => SetzeAlle(true);

    private void Keine_Click(object sender, RoutedEventArgs e) => SetzeAlle(false);

    private void SetzeAlle(bool wert)
    {
        foreach (var zeile in _zeilen)
            zeile.Uebernehmen = wert;

        // Die Zeilen melden keine Aenderung; ein Neuaufbau ist hier billiger
        // als INotifyPropertyChanged fuer eine einzige Ankreuzspalte.
        Liste.ItemsSource = null;
        Liste.ItemsSource = _zeilen;
    }

    private void Uebernehmen_Click(object sender, RoutedEventArgs e)
    {
        Gewaehlt = _zeilen.Where(z => z.Uebernehmen).ToList();
        DialogResult = true;
    }
}
