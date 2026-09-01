using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

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
    private readonly IParcelLookup _parcels;
    private readonly DossierParcelLookupUseCase _lookup;
    private readonly IDirectoryLookup _directory;
    private readonly IReadOnlyDictionary<string, Guid> _holdingIdsByName;
    private readonly IReadOnlyList<string> _projectShaftNumbers;

    private DossierDefinition? _ergebnis;

    /// <summary>
    /// Die bestaetigte Parzellennummer des Parzellendienstes. Bewusst NICHT
    /// aus dem Eingabefeld: dort kann inzwischen etwas anderes stehen, und die
    /// Schaechte sollen zu der Parzelle gehoeren, die wirklich gefunden wurde.
    /// </summary>
    private string? _parzellenNummer;
    private readonly List<CheckBox> _leitungen = new();
    private TextBlock? _schachtZeile;

    private DossierParcelLookupWindow(
        IParcelLookup parcels,
        DossierParcelLookupUseCase lookup,
        IDirectoryLookup directory,
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers)
    {
        InitializeComponent();

        _parcels = parcels;
        _lookup = lookup;
        _directory = directory;
        _holdingIdsByName = holdingIdsByName;
        _projectShaftNumbers = projectShaftNumbers;

        DirectoryBox.IsEnabled = _directory.IsConfigured;
        DirectoryBox.ToolTip = _directory.IsConfigured
            ? _directory.Attribution + " — Kontingent 1000 Abfragen im Monat"
            : "Dafür fehlt der Schlüssel von search.ch beziehungsweise local.ch. "
                + "Er wird in den Einstellungen hinterlegt.";

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
        IReadOnlyDictionary<string, Guid> holdingIdsByName,
        IReadOnlyList<string> projectShaftNumbers)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(holdingIdsByName);
        ArgumentNullException.ThrowIfNull(projectShaftNumbers);

        var window = new DossierParcelLookupWindow(
            parcels, lookup, directory, holdingIdsByName, projectShaftNumbers)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true || window._ergebnis is null)
            return null;

        var gewaehlt = window.GewaehlteLeitungen();

        return new DossierParcelLookupChoice(
            window._ergebnis,
            gewaehlt,
            ParcelHoldingAndShaftMatcher.ShaftsForParcel(
                gewaehlt, projectShaftNumbers, window._parzellenNummer));
    }

    private IReadOnlyList<string> GewaehlteLeitungen()
        => _leitungen
            .Where(c => c.IsChecked == true)
            .Select(c => (string)c.Tag)
            .ToList();

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
        _parzellenNummer = null;

        try
        {
            var fortschritt = new Progress<string>(text => StatusText.Text = text);
            var ergebnis = await _lookup.RunAsync(
                gemeinde.BfsNr, nummer, _holdingIdsByName.Keys.ToList(), fortschritt);

            OwnerDirectoryFillResult? verzeichnis = null;
            if (ergebnis.Dossier is not null
                && DirectoryBox.IsChecked == true
                && _directory.IsConfigured)
            {
                verzeichnis = await SucheVerzeichnisAsync(ergebnis.Dossier);
            }

            if (verzeichnis?.IsUnavailable == true)
            {
                ergebnis = ergebnis with
                {
                    Warnings = [.. ergebnis.Warnings, "Verzeichnis: " + verzeichnis.Unavailable]
                };
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
    /// Telefon und Mail je Eigentuemer. Kontingent und die Regel „nur ein
    /// eindeutiger Treffer" liegen im geprueften
    /// <see cref="OwnerDirectoryLookupUseCase"/> — im Fenstercode waren sie
    /// von keiner Pruefung gedeckt, obwohl sie eine Nutzungsgrenze sind.
    /// </summary>
    private async Task<OwnerDirectoryFillResult> SucheVerzeichnisAsync(DossierDefinition dossier)
    {
        StatusText.Text = "Verzeichnis: Telefon und Mail werden gesucht…";

        return await new OwnerDirectoryLookupUseCase(_directory)
            .FillWithResultAsync(dossier);
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
        _parzellenNummer = ergebnis.Parcel?.Number;
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
    /// <summary>
    /// Nur Leitungen, die das Hauptprojekt wirklich fuehrt. Was der Kanton
    /// kennt, das Projekt aber nicht aufgenommen hat, gehoert nicht ins
    /// Dossier — es wird nur als Zahl gemeldet, damit die Luecke sichtbar ist.
    /// </summary>
    private void ZeigeLeitungen(IReadOnlyList<ProposedHolding> haltungen)
    {
        var imProjekt = haltungen.Where(h => h.InProject).ToList();
        var fremde = haltungen.Count - imProjekt.Count;

        ResultPanel.Children.Add(new TextBlock
        {
            Text = imProjekt.Count == 0
                ? "Im Projekt gibt es zu dieser Parzelle keine aufgenommenen Leitungen."
                : imProjekt.Count + " aufgenommene Leitungen im Projekt",
            Margin = new Thickness(0, 14, 0, 2)
        });

        foreach (var haltung in imProjekt)
        {
            // "Lage" heisst: der Kanton fuehrt sie. "Name" heisst: nur ihr
            // Knotenname zeigt auf die Parzelle — so heissen die privaten
            // Hausanschluesse, die der Kanton nicht kennt.
            var herkunft = haltung.Origin == "Name"
                ? "aus dem Leitungsnamen — privat angenommen"
                : haltung.IsPrivate ? "beim Kanton, privat" : "beim Kanton, öffentlich";

            var kasten = new CheckBox
            {
                Content = haltung.Designation + "   (" + herkunft + ")",
                Tag = haltung.Designation,
                IsChecked = haltung.Preselected,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                Margin = new Thickness(0, 2, 0, 0)
            };

            kasten.Checked += (_, _) => ZeigeSchaechte();
            kasten.Unchecked += (_, _) => ZeigeSchaechte();

            _leitungen.Add(kasten);
            ResultPanel.Children.Add(kasten);
        }

        if (fremde > 0)
        {
            ResultPanel.Children.Add(new TextBlock
            {
                Text = fremde == 1
                    ? "Der Kanton kennt hier eine weitere Leitung, die das Projekt nicht führt."
                    : $"Der Kanton kennt hier {fremde} weitere Leitungen, die das Projekt nicht führt.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }

        _schachtZeile = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            FontSize = 13,
            Margin = new Thickness(0, 12, 0, 0)
        };

        ResultPanel.Children.Add(_schachtZeile);
        ZeigeSchaechte();
    }

    /// <summary>
    /// Die Schaechte kommen aus den Knoten der angehakten Leitungen UND aus
    /// den Schachtnamen, die selbst auf die Parzelle zeigen. Die erste Haelfte
    /// aendert sich mit jedem Haken, die zweite steht fest. Aufgenommen wird
    /// nur, was das Hauptprojekt als Schacht fuehrt.
    /// </summary>
    private void ZeigeSchaechte()
    {
        if (_schachtZeile is null)
            return;

        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            GewaehlteLeitungen(), _projectShaftNumbers, _parzellenNummer);

        _schachtZeile.Text = schaechte.Count == 0
            ? "Keine Schächte — das Projekt führt auf dieser Parzelle keinen Schacht."
            : schaechte.Count == 1
                ? "1 Schacht kommt mit: " + schaechte[0]
                : schaechte.Count + " Schächte kommen mit: " + string.Join(", ", schaechte);
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
