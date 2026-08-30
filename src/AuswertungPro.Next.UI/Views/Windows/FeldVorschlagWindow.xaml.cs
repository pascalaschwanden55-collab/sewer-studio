using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt einen nachgeschlagenen Feldwert und uebernimmt ihn erst nach
/// ausdruecklicher Bestaetigung. Bei mehreren Treffern entscheidet der
/// Mensch — geraten wird nie.
/// </summary>
public partial class FeldVorschlagWindow : Window
{
    /// <summary>Der bestaetigte Vorschlag, oder null bei Abbruch.</summary>
    public FeldVorschlag? Uebernommen { get; private set; }

    public FeldVorschlagWindow(
        string schachtnummer,
        string feldname,
        FeldNachschlagErgebnis ergebnis)
    {
        InitializeComponent();

        KopfText.Text = $"Schacht {schachtnummer} · Feld \"{feldname}\"";
        Zeige(ergebnis);
    }

    private void Zeige(FeldNachschlagErgebnis ergebnis)
    {
        switch (ergebnis)
        {
            case FeldNachschlagErgebnis.Gefunden gefunden:
                TrefferText.Text = gefunden.Vorschlag.Wert;
                QuelleText.Text = $"Quelle: {gefunden.Vorschlag.QuelleKlartext}";
                Uebernommen = gefunden.Vorschlag;
                break;

            case FeldNachschlagErgebnis.Mehrdeutig mehrdeutig:
                TrefferBereich.Visibility = Visibility.Collapsed;
                AuswahlBereich.Visibility = Visibility.Visible;
                KandidatenListe.ItemsSource = mehrdeutig.Kandidaten;
                QuelleText.Text = mehrdeutig.Kandidaten.Count > 0
                    ? $"Quelle: {mehrdeutig.Kandidaten[0].QuelleKlartext}"
                    : string.Empty;
                // Erst nach einer Auswahl uebernehmbar.
                UebernehmenKnopf.IsEnabled = false;
                break;

            case FeldNachschlagErgebnis.NichtGefunden nicht:
                NurMeldung(nicht.Grund);
                break;

            case FeldNachschlagErgebnis.Gedrosselt:
                NurMeldung(
                    "Der Auskunftsdienst des Kantons hat zu viele Abfragen in kurzer Zeit "
                    + "erhalten und antwortet gerade nicht. Bitte spaeter erneut versuchen.");
                break;

            case FeldNachschlagErgebnis.Fehler fehler:
                NurMeldung($"Die Abfrage ist fehlgeschlagen: {fehler.Meldung}");
                break;
        }
    }

    /// <summary>Kein Treffer: nur die Meldung und ein Schliessen-Knopf.</summary>
    private void NurMeldung(string text)
    {
        TrefferText.Text = text;
        QuelleText.Visibility = Visibility.Collapsed;
        UebernehmenKnopf.Visibility = Visibility.Collapsed;
        AbbrechenKnopf.Content = "Schliessen";
        Uebernommen = null;
    }

    private void KandidatenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        Uebernommen = KandidatenListe.SelectedItem as FeldVorschlag;
        UebernehmenKnopf.IsEnabled = Uebernommen is not null;
    }

    private void Uebernehmen_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (Uebernommen is null)
            return;

        DialogResult = true;
        Close();
    }
}
