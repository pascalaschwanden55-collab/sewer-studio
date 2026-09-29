using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6 («Hilfe-Menü, F1, Tastenkürzel, Handbuch»): Das Handbuch als
/// eigenes, NICHT-modales Fenster (ersetzt den früheren Reiter «Hilfe» in den Einstellungen).
/// Inhaltsverzeichnis links (gruppiert wie die Seitenleiste: Projekt/Daten/Bewertung/System, dazu
/// ein eigener Abschnitt «Für Fachleute»), Text rechts, Suchfeld im Kopf. Der Inhalt selbst kommt
/// ausschliesslich aus <see cref="HandbuchInhalt"/> (WPF-frei, dadurch ohne WPF-Testprozess prüfbar);
/// dieses Fenster stellt nur dar und filtert.
///
/// <see cref="ZeigeAn"/> ist die EINE Einstiegsstelle (Hilfe-Menü, F1, der Knopf «Handbuch öffnen»
/// in den Einstellungen) und hält das Fenster als Einzelstück: ein bereits offenes Handbuch wird
/// nur zum gewünschten Abschnitt weitergeschaltet und aktiviert statt ein zweites Mal geöffnet
/// (gleiches Muster wie <c>DataPage.ShowOrUpdateBeobachtungenWindow</c>).
/// </summary>
public partial class HandbuchWindow : Window
{
    private static HandbuchWindow? _instanz;

    /// <summary>Nur fuer Tests (InternalsVisibleTo): das aktuell offene Einzelstueck, falls vorhanden.</summary>
    internal static HandbuchWindow? Aktuelles => _instanz;

    private readonly ObservableCollection<HandbuchTocEintrag> _eintraege = new();
    private readonly ICollectionView _ansicht;
    private HandbuchAbschnitt _aktuellerAbschnitt = HandbuchInhalt.Abschnitte[0];

    public HandbuchWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        foreach (var abschnitt in HandbuchInhalt.Abschnitte)
        {
            var gruppe = abschnitt.IstFachlich
                ? "Für Fachleute"
                : ShellNavigationTitles.Anzeige(ShellNavigationGroups.GroupOf(abschnitt.Schluessel));
            _eintraege.Add(new HandbuchTocEintrag(abschnitt, gruppe));
        }

        _ansicht = CollectionViewSource.GetDefaultView(_eintraege);
        _ansicht.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HandbuchTocEintrag.Gruppe)));
        InhaltsverzeichnisListe.ItemsSource = _ansicht;

        ZeigeAbschnitt(_aktuellerAbschnitt.Schluessel);
    }

    /// <summary>
    /// Öffnet das Handbuch beim genannten Abschnitt (bei einer Programmseite deren <c>NavItem.Title</c>)
    /// - unbekannt oder leer landet auf «Übersicht» (<see cref="HandbuchInhalt.Finde"/>). Ist das
    /// Fenster bereits offen, wird nur weitergeschaltet und aktiviert statt neu erzeugt.
    /// </summary>
    public static void ZeigeAn(string? seitenSchluessel = null, Window? owner = null)
    {
        if (_instanz is { IsLoaded: true } offen)
        {
            offen.ZeigeAbschnitt(seitenSchluessel);
            offen.Activate();
            return;
        }

        var neu = new HandbuchWindow();
        if (owner is not null)
            neu.Owner = owner;
        _instanz = neu;
        neu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_instanz, neu))
                _instanz = null;
        };
        neu.ZeigeAbschnitt(seitenSchluessel);
        neu.Show();
    }

    public void ZeigeAbschnitt(string? schluessel)
    {
        _aktuellerAbschnitt = HandbuchInhalt.Finde(schluessel);

        var eintrag = _eintraege.FirstOrDefault(e => e.Abschnitt.Schluessel == _aktuellerAbschnitt.Schluessel);
        if (eintrag is not null)
            InhaltsverzeichnisListe.SelectedItem = eintrag;

        RenderAbschnitt(_aktuellerAbschnitt);
    }

    private void RenderAbschnitt(HandbuchAbschnitt abschnitt)
    {
        AbschnittTitelText.Text = abschnitt.Titel;

        var text = new TextBlock
        {
            Text = abschnitt.Text,
            TextWrapping = TextWrapping.Wrap
        };
        text.SetResourceReference(TextBlock.FontSizeProperty, "TextS");
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

        if (!abschnitt.IstFachlich)
        {
            AbschnittInhaltHost.Content = text;
            return;
        }

        // Entwicklerabschnitt: standardmässig zu, der Kanalinspekteur soll ihn nicht ungefragt
        // vor der Nase haben (Brief: «standardmässig zu»).
        AbschnittInhaltHost.Content = new Expander
        {
            Header = "Technische Details anzeigen",
            IsExpanded = false,
            Content = text
        };
    }

    private void InhaltsverzeichnisListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InhaltsverzeichnisListe.SelectedItem is HandbuchTocEintrag eintrag)
        {
            _aktuellerAbschnitt = eintrag.Abschnitt;
            RenderAbschnitt(_aktuellerAbschnitt);
        }
    }

    private void SucheBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var suche = SucheBox.Text?.Trim();
        if (string.IsNullOrEmpty(suche))
        {
            _ansicht.Filter = null;
            return;
        }

        _ansicht.Filter = o => o is HandbuchTocEintrag eintrag
            && (eintrag.Titel.Contains(suche, StringComparison.OrdinalIgnoreCase)
                || eintrag.Abschnitt.Text.Contains(suche, StringComparison.OrdinalIgnoreCase));
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>Ein Eintrag im Inhaltsverzeichnis. Traegt den ganzen Abschnitt mit, damit die Suche
    /// auch den Fliesstext durchsuchen kann, nicht nur den Titel.</summary>
    public sealed class HandbuchTocEintrag
    {
        public HandbuchTocEintrag(HandbuchAbschnitt abschnitt, string gruppe)
        {
            Abschnitt = abschnitt;
            Gruppe = gruppe;
        }

        public HandbuchAbschnitt Abschnitt { get; }
        public string Titel => Abschnitt.Titel;
        public string Gruppe { get; }
    }
}
