using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Zeigt, was ein Dossier ergaenzen wuerde, und laesst den Menschen
/// entscheiden.
///
/// Bewusst mit Bestaetigung statt still: nur so laesst sich beim ersten Lauf
/// auf einem bestehenden Dossier „neu dazugekommen" von „habe ich absichtlich
/// entfernt" unterscheiden. Was hier abgehakt wird, gilt als abgelehnt und
/// wird nie wieder angeboten.
/// </summary>
public partial class DossierRefreshWindow : Window
{
    private readonly DossierRefreshProposal _vorschlag;
    private readonly List<CheckBox> _leitungen = new();
    private readonly List<CheckBox> _schaechte = new();

    private DossierRefreshWindow(string dossierName, DossierRefreshProposal vorschlag)
    {
        InitializeComponent();

        _vorschlag = vorschlag;

        HeaderText.Text = $"Neu im Projekt für „{dossierName}“. "
            + "Was Sie abhaken, wird beim nächsten Nachführen nicht wieder vorgeschlagen.";

        Baue();

        FootText.Text = "Nichts wird entfernt. Texte, Themen, Eigentümer und Plan "
            + "bleiben unberührt.";
    }

    /// <summary>Die Auswahl, oder null bei Abbruch.</summary>
    public DossierRefreshChoice? Choice { get; private set; }

    public static DossierRefreshChoice? ShowFor(
        string dossierName, DossierRefreshProposal vorschlag)
    {
        ArgumentNullException.ThrowIfNull(vorschlag);

        var fenster = new DossierRefreshWindow(dossierName, vorschlag)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return fenster.ShowDialog() == true ? fenster.Choice : null;
    }

    private void Baue()
    {
        if (_vorschlag.NewHoldings.Count > 0)
        {
            Ueberschrift(_vorschlag.NewHoldings.Count == 1 ? "Leitung" : "Leitungen");

            foreach (var leitung in _vorschlag.NewHoldings)
                _leitungen.Add(Haken(leitung.Designation, leitung));
        }

        if (_vorschlag.NewShafts.Count > 0)
        {
            Ueberschrift(_vorschlag.NewShafts.Count == 1 ? "Schacht" : "Schächte");

            foreach (var schacht in _vorschlag.NewShafts)
                _schaechte.Add(Haken(schacht, schacht));
        }
    }

    private void Ueberschrift(string text)
        => ItemPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, ItemPanel.Children.Count == 0 ? 0 : 14, 0, 6)
        });

    private CheckBox Haken(string beschriftung, object marke)
    {
        var haken = new CheckBox
        {
            Content = beschriftung,
            Tag = marke,
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 5)
        };

        ItemPanel.Children.Add(haken);
        return haken;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        Choice = new DossierRefreshChoice(
            _leitungen
                .Where(c => c.IsChecked == true)
                .Select(c => (RefreshableHolding)c.Tag!)
                .ToList(),
            _schaechte
                .Where(c => c.IsChecked == true)
                .Select(c => (string)c.Tag!)
                .ToList());

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
