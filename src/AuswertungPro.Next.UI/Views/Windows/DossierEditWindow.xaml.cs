using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Stammdaten einer Liegenschaft. Das Fenster arbeitet auf einer Arbeitskopie:
/// erst beim Speichern werden die Werte in das Dossier uebernommen, damit ein
/// Abbrechen wirklich nichts veraendert.
/// </summary>
public partial class DossierEditWindow : Window
{
    private readonly DossierDefinition _target;
    private readonly ObservableCollection<DossierOwnerRow> _owners = new();
    private readonly ObservableCollection<DossierTopicRow> _topics = new();
    private readonly ObservableCollection<DossierChangeRow> _changes = new();
    private string _planPath = "";

    private DossierEditWindow(DossierDefinition target, bool isNew)
    {
        InitializeComponent();
        _target = target;

        Title = isNew ? "Neue Liegenschaft" : "Liegenschaft bearbeiten";
        Load();
    }

    /// <summary>Zeigt das Fenster. Liefert true, wenn gespeichert wurde.</summary>
    public static bool ShowFor(DossierDefinition definition, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var window = new DossierEditWindow(definition, isNew)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true;
    }

    private void Load()
    {
        NameBox.Text = _target.Name;
        ParcelBox.Text = _target.ParcelNumbers;
        HouseBox.Text = _target.HouseNumbers;
        AddressBox.Text = _target.Address;
        ZipBox.Text = _target.PostalCode;
        TownBox.Text = _target.Town;

        OwnerBox.Text = _target.OwnerName;
        OwnerAddressBox.Text = _target.OwnerAddress;
        ContactBox.Text = _target.ContactName;
        PhoneBox.Text = _target.ContactPhone;
        MailBox.Text = _target.ContactMail;
        OccupancyBox.Text = _target.Occupancy;

        FileNoteBox.Text = _target.FileNote;
        RevisionBox.Text = _target.Revision;
        DeadlineOverrideBox.Text = _target.ResponseDeadlineOverride ?? "";

        _topics.Clear();
        foreach (var thema in _target.Topics ?? new())
        {
            if (thema is not null)
                _topics.Add(new DossierTopicRow
                {
                    Title = thema.Title,
                    Text = thema.Text,
                    ColorHex = thema.ColorHex,
                    StyleRanges = KopiereFormat(thema.StyleRanges)
                });
        }

        _changes.Clear();
        foreach (var zeile in _target.Changes ?? new())
        {
            if (zeile is null)
                continue;

            _changes.Add(new DossierChangeRow
            {
                Version = zeile.Version,
                Date = zeile.Date,
                Visum = zeile.Visum,
                Change = zeile.Change
            });
        }

        TopicsGrid.ItemsSource = _topics;
        ChangesGrid.ItemsSource = _changes;

        _owners.Clear();
        foreach (var owner in _target.Owners)
        {
            // Arbeitskopie: ein Abbrechen darf die gespeicherten Zeilen nicht veraendern.
            _owners.Add(new DossierOwnerRow
            {
                HouseNumber = owner.HouseNumber,
                ParcelNumber = owner.ParcelNumber,
                Name = owner.Name,
                Phone = owner.Phone,
                Mail = owner.Mail,
                Occupancy = owner.Occupancy
            });
        }

        OwnersGrid.ItemsSource = _owners;

        _planPath = _target.OverviewPlanPath;
        ShowPlanPath();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? "";
        if (name.Length == 0)
        {
            // Ohne Namen gaebe es keinen sinnvollen Ordner- und Listeneintrag.
            HintText.Text = "Bitte einen Namen eingeben.";
            NameBox.Focus();
            return;
        }

        _target.Name = name;
        _target.ParcelNumbers = Trim(ParcelBox.Text);
        _target.HouseNumbers = Trim(HouseBox.Text);
        _target.Address = Trim(AddressBox.Text);
        _target.PostalCode = Trim(ZipBox.Text);
        _target.Town = Trim(TownBox.Text);

        _target.OwnerName = Trim(OwnerBox.Text);
        _target.OwnerAddress = Trim(OwnerAddressBox.Text);
        _target.ContactName = Trim(ContactBox.Text);
        _target.ContactPhone = Trim(PhoneBox.Text);
        _target.ContactMail = Trim(MailBox.Text);
        _target.Occupancy = Trim(OccupancyBox.Text);

        _target.FileNote = Trim(FileNoteBox.Text);

        var revision = Trim(RevisionBox.Text);
        _target.Revision = revision.Length == 0 ? "A" : revision;

        _target.ResponseDeadlineOverride = NullIfEmpty(DeadlineOverrideBox.Text);

        // Die Raster geben die letzte Zelle erst beim Fokuswechsel frei.
        OwnersGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        TopicsGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        ChangesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        _target.Owners = _owners
            .Where(owner => owner.HasContent)
            .ToList();

        // Ein Thema ohne Titel findet im Dossier keinen Platz und stuende als
        // leere Tabellenzeile im Dokument.
        _target.Topics = _topics
            .Where(thema => !string.IsNullOrWhiteSpace(thema.Title))
            .Select(thema => new DossierTopicRow
            {
                Title = thema.Title.Trim(),
                Text = thema.Text ?? "",
                ColorHex = thema.ColorHex,
                StyleRanges = KopiereFormat(thema.StyleRanges)
            })
            .ToList();

        _target.Changes = _changes
            .Where(DossierChangeRows.HasContent)
            .ToList();

        _target.OverviewPlanPath = _planPath;

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string Trim(string? value) => value?.Trim() ?? "";

    private static System.Collections.Generic.List<DossierTextStyleRange> KopiereFormat(
        System.Collections.Generic.IEnumerable<DossierTextStyleRange>? ranges)
        => (ranges ?? System.Linq.Enumerable.Empty<DossierTextStyleRange>())
            .Select(r => new DossierTextStyleRange
            {
                Start = r.Start,
                Length = r.Length,
                ColorHex = r.ColorHex,
                Bold = r.Bold,
                Italic = r.Italic,
                Underline = r.Underline
            })
            .ToList();

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? null : trimmed;
    }

    private void OnAddOwner(object sender, RoutedEventArgs e)
    {
        var row = new DossierOwnerRow
        {
            // Die erste Zeile uebernimmt die Angaben der Liegenschaft als Vorschlag.
            HouseNumber = _owners.Count == 0 ? Trim(HouseBox.Text) : "",
            ParcelNumber = _owners.Count == 0 ? Trim(ParcelBox.Text) : ""
        };

        _owners.Add(row);
        OwnersGrid.SelectedItem = row;
    }

    private void OnAddTopic(object sender, RoutedEventArgs e)
    {
        var zeile = new DossierTopicRow();
        _topics.Add(zeile);
        TopicsGrid.SelectedItem = zeile;
    }

    private void OnRemoveTopic(object sender, RoutedEventArgs e)
    {
        if (TopicsGrid.SelectedItem is DossierTopicRow zeile)
            _topics.Remove(zeile);
    }

    private void OnAddChange(object sender, RoutedEventArgs e)
    {
        var zeile = new DossierChangeRow
        {
            // Fortlaufende Nummer und heutiges Datum als Vorschlag.
            Version = (_changes.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Date = DateTime.Today.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)
        };

        _changes.Add(zeile);
        ChangesGrid.SelectedItem = zeile;
    }

    private void OnRemoveChange(object sender, RoutedEventArgs e)
    {
        if (ChangesGrid.SelectedItem is DossierChangeRow zeile)
            _changes.Remove(zeile);
    }

    private void OnRemoveOwner(object sender, RoutedEventArgs e)
    {
        if (OwnersGrid.SelectedItem is DossierOwnerRow row)
            _owners.Remove(row);
    }

    private void OnChoosePlan(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Werkleitungsplan wählen",
            Filter = "Bilder (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _planPath = dialog.FileName;
        ShowPlanPath();
    }

    private void OnClearPlan(object sender, RoutedEventArgs e)
    {
        _planPath = "";
        ShowPlanPath();
    }

    private void ShowPlanPath()
    {
        if (_planPath.Length == 0)
        {
            PlanPathText.Text = "Kein Bild gewählt — Kapitel 1 bleibt leer.";
            return;
        }

        PlanPathText.Text = File.Exists(_planPath)
            ? Path.GetFileName(_planPath)
            : Path.GetFileName(_planPath) + "  (Datei nicht gefunden)";
    }
}
