using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Was der Benutzer aus der Abfrage uebernommen hat.</summary>
public sealed record DossierParcelLookupChoice(
    DossierDefinition Dossier,
    IReadOnlyList<string> SelectedHoldingDesignations);

/// <summary>
/// Fragt Gemeinde und Parzelle ab und fuellt daraus alles vor, was die
/// oeffentlichen Dienste des Kantons hergeben: Eigentuemer, Adresse und die
/// Leitungen auf der Parzelle.
///
/// Die Regeln liegen im Anwendungsfall; dieses Fenster sammelt nur ein, zeigt
/// das Ergebnis und laesst waehlen. Telefon und Mail werden nur auf
/// ausdruecklichen Wunsch und nur fuer diese eine Liegenschaft gesucht — die
/// Nutzungsbedingungen des Verzeichnisses verbieten Massenabfragen.
/// </summary>
public partial class DossierParcelLookupWindow : Window
{
    private const int MaxVerzeichnisAbfragen = 5;

    private readonly IParcelLookup _parcels;
    private readonly DossierParcelLookupUseCase _lookup;
    private readonly IDirectoryLookup _directory;
    private readonly IReadOnlyDictionary<string, Guid> _holdingIdsByName;

    private DossierDefinition? _ergebnis;
    private readonly List<CheckBox> _leitungen = new();

    private DossierParcelLookupWindow(
        IParcelLookup parcels,
        DossierParcelLookupUseCase lookup,
        IDirectoryLookup directory,
        IReadOnlyDictionary<string, Guid> holdingIdsByName)
    {
        InitializeComponent();

        _parcels = parcels;
        _lookup = lookup;
        _directory = directory;
        _holdingIdsByName = holdingIdsByName;

        DirectoryBox.IsEnabled = _directory.IsConfigured;
        DirectoryBox.ToolTip = _directory.IsConfigured
            ? _directory.Attribution
            : "Dafür fehlt der Schlüssel von search.ch. Er wird in den Einstellungen hinterlegt.";

        Loaded += async (_, _) => await LadeGemeindenAsync();
    }

    /// <summary>
    /// Zeigt die Abfrage. Liefert die uebernommenen Angaben, ein leeres Dossier
    /// (wenn ohne Abfrage angelegt wird) oder null beim Abbrechen.
    /// </summary>
    public static DossierParcelLookupChoice? ShowFor(
        IParcelLookup parcels,
        DossierParcelLookupUseCase lookup,
        IDirectoryLookup directory,
        IReadOnlyDictionary<string, Guid> holdingIdsByName)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(holdingIdsByName);

        var window = new DossierParcelLookupWindow(parcels, lookup, directory, holdingIdsByName)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true || window._ergebnis is null)
            return null;

        return new DossierParcelLookupChoice(
            window._ergebnis,
            window._leitungen
                .Where(c => c.IsChecked == true)
                .Select(c => (string)c.Tag)
                .ToList());
    }

    private async Task LadeGemeindenAsync()
    {
        try
        {
            var gemeinden = await _parcels.ListMunicipalitiesAsync();
            MunicipalityBox.ItemsSource = gemeinden;

            if (gemeinden.Count > 0)
                MunicipalityBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Die Gemeindeliste konnte nicht geladen werden: " + ex.Message;
        }
    }

    private async void OnLookup(object sender, RoutedEventArgs e)
    {
        if (MunicipalityBox.SelectedItem is not Municipality gemeinde)
        {
            StatusText.Text = "Bitte zuerst eine Gemeinde wählen.";
            return;
        }

        var nummer = ParcelBox.Text?.Trim() ?? string.Empty;
        if (nummer.Length == 0)
        {
            StatusText.Text = "Bitte eine Parzellennummer eingeben.";
            return;
        }

        LookupButton.IsEnabled = false;
        AcceptButton.IsEnabled = false;
        _ergebnis = null;

        try
        {
            var fortschritt = new Progress<string>(text => StatusText.Text = text);
            var ergebnis = await _lookup.RunAsync(gemeinde.BfsNr, nummer, fortschritt);

            if (ergebnis.Dossier is not null
                && DirectoryBox.IsChecked == true
                && _directory.IsConfigured)
            {
                await SucheVerzeichnisAsync(ergebnis.Dossier);
            }

            Zeige(ergebnis);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Die Abfrage ist fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            LookupButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Telefon und Mail je Eigentuemer — eine Abfrage je Person und hoechstens
    /// eine Handvoll. Nur ein EINDEUTIGER Treffer wird uebernommen: eine
    /// geratene Nummer im Brief waere schlimmer als eine leere Zelle.
    /// </summary>
    private async Task SucheVerzeichnisAsync(DossierDefinition dossier)
    {
        var gesucht = 0;

        foreach (var eigentuemer in dossier.Owners)
        {
            if (gesucht >= MaxVerzeichnisAbfragen)
                break;

            if (string.IsNullOrWhiteSpace(eigentuemer.Name))
                continue;

            gesucht++;
            StatusText.Text = "Verzeichnis: " + eigentuemer.Name;

            DirectoryLookupResult treffer;
            try
            {
                treffer = await _directory.FindAsync(eigentuemer.Name, dossier.Town);
            }
            catch (Exception)
            {
                continue;
            }

            if (treffer.Unique is not { } eintrag)
                continue;

            eigentuemer.Phone = eintrag.Phone;
            eigentuemer.Mail = eintrag.Mail;
        }
    }

    private void Zeige(DossierParcelLookupResult ergebnis)
    {
        ResultPanel.Children.Clear();
        _leitungen.Clear();

        foreach (var warnung in ergebnis.Warnings)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = "⚠ " + warnung,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        if (ergebnis.Dossier is null)
        {
            StatusText.Text = "Nichts gefunden.";
            return;
        }

        _ergebnis = ergebnis.Dossier;
        AcceptButton.IsEnabled = true;
        StatusText.Text = "Gefunden.";

        Zeile("Name", ergebnis.Dossier.Name);
        Zeile("Parzelle", ergebnis.Dossier.ParcelNumbers);
        Zeile("Gemeinde", ergebnis.Dossier.Municipality);
        Zeile("Adresse", string.Join(" ", new[]
        {
            ergebnis.Dossier.Address, ergebnis.Dossier.HouseNumbers
        }.Where(t => !string.IsNullOrWhiteSpace(t))));
        Zeile("Ort", string.Join(" ", new[]
        {
            ergebnis.Dossier.PostalCode, ergebnis.Dossier.Town
        }.Where(t => !string.IsNullOrWhiteSpace(t))));

        ResultPanel.Children.Add(new TextBlock
        {
            Text = ergebnis.Dossier.Owners.Count == 1
                ? "Eigentümer"
                : ergebnis.Dossier.Owners.Count + " Eigentümerparteien",
            Margin = new Thickness(0, 14, 0, 2)
        });

        foreach (var eigentuemer in ergebnis.Dossier.Owners)
        {
            var text = eigentuemer.Name;
            if (!string.IsNullOrWhiteSpace(eigentuemer.Phone))
                text += "   ☎ " + eigentuemer.Phone;
            if (!string.IsNullOrWhiteSpace(eigentuemer.Mail))
                text += "   ✉ " + eigentuemer.Mail;

            ResultPanel.Children.Add(new TextBlock
            {
                Text = "• " + text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                FontSize = 13
            });
        }

        if (DirectoryBox.IsChecked == true && _directory.IsConfigured)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = _directory.Attribution,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        ZeigeLeitungen(ergebnis.Holdings);
    }

    /// <summary>
    /// Die Leitungen auf der Parzelle. Angehakt werden nur die, die es im
    /// Projekt wirklich gibt — eine Leitung, die das Projekt nicht kennt, kann
    /// nicht ins Dossier.
    /// </summary>
    private void ZeigeLeitungen(IReadOnlyList<NetworkHolding> haltungen)
    {
        ResultPanel.Children.Add(new TextBlock
        {
            Text = haltungen.Count == 0
                ? "Der Kanton kennt auf dieser Parzelle keine Leitungen."
                : haltungen.Count + " Leitungen auf der Parzelle",
            Margin = new Thickness(0, 14, 0, 2)
        });

        foreach (var haltung in haltungen)
        {
            var bekannt = _holdingIdsByName.ContainsKey(haltung.Designation);

            var kasten = new CheckBox
            {
                Content = haltung.Designation
                    + (bekannt ? string.Empty : "   (nicht im Projekt)")
                    + (haltung.IsPrivate ? "   privat" : string.Empty),
                Tag = haltung.Designation,
                IsChecked = bekannt,
                IsEnabled = bekannt,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            };

            _leitungen.Add(kasten);
            ResultPanel.Children.Add(kasten);
        }
    }

    private void Zeile(string beschriftung, string wert)
    {
        ResultPanel.Children.Add(new TextBlock
        {
            Text = beschriftung,
            Margin = new Thickness(0, 8, 0, 0)
        });

        ResultPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(wert) ? "—" : wert,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        });
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _ergebnis = new DossierDefinition();
        _leitungen.Clear();
        DialogResult = true;
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (_ergebnis is null)
            return;

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
