using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Ein waehlbarer Schacht in der Auswahlliste.</summary>
public sealed partial class DossierShaftChoice : ObservableObject
{
    public DossierShaftChoice(SchachtRecord record, bool chosen)
    {
        // Dieselbe Nummernregel wie Tabelle und Nachfuehren.
        ShaftNumber = DossierShaftNumberPolicy.NumberOf(record);

        Street = (record.GetFieldValue(FieldKeys.Street) ?? "").Trim();
        Funktion = (record.GetFieldValue("Funktion") ?? "").Trim();

        var material = (record.GetFieldValue("Material") ?? "").Trim();
        var form = (record.GetFieldValue("Schachtform") ?? "").Trim();

        Detail = string.Join(" · ", new[] { Street, material, form }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        _isChosen = chosen;
    }

    /// <summary>Die gespeicherte Kennung dieses Schachts.</summary>
    public string ShaftNumber { get; }

    public string Street { get; }
    public string Funktion { get; }
    public string Detail { get; }

    /// <summary>Gruppenueberschrift, je nach gewaehlter Gruppierung.</summary>
    public string GroupLabel { get; set; } = "";

    [ObservableProperty]
    private bool _isChosen;
}

/// <summary>
/// Auswahl der Schaechte einer Liegenschaft. Zwilling zum
/// <see cref="DossierHoldingPickerWindow"/>: bei einem ganzen Gebiet sind das
/// schnell 40 Zeilen, deshalb Suchfeld und Gruppierung nach Strasse oder Funktion.
///
/// Ein Schacht ohne Nummer erscheint gar nicht erst: Das Dossier verweist ueber
/// die Nummer, ein Schacht ohne sie waere nicht speicherbar.
/// </summary>
public partial class DossierShaftPickerWindow : Window
{
    private readonly List<DossierShaftChoice> _all;
    private readonly ICollectionView _view;

    private DossierShaftPickerWindow(Project project, IReadOnlyCollection<string> chosen)
    {
        InitializeComponent();

        var chosenSet = new HashSet<string>(
            chosen.Select(n => (n ?? "").Trim()).Where(n => n.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        // Ohne Nummer nicht speicherbar, doppelte Nummern nicht
        // auseinanderzuhalten — beides gehoert nicht in die Auswahl.
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _all = project.SchaechteData
            .Select(record => new DossierShaftChoice(
                record, chosenSet.Contains(DossierShaftNumberPolicy.NumberOf(record))))
            .Where(choice => choice.ShaftNumber.Length > 0 && gesehen.Add(choice.ShaftNumber))
            .OrderBy(c => c.ShaftNumber, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var choice in _all)
            choice.PropertyChanged += OnChoiceChanged;

        ShaftList.ItemsSource = _all;
        _view = CollectionViewSource.GetDefaultView(_all);
        _view.Filter = Matches;

        UpdateCount();
    }

    /// <summary>
    /// Zeigt die Auswahl. Liefert die neuen Schachtnummern oder null bei Abbruch.
    /// </summary>
    public static List<string>? ShowFor(Project project, IReadOnlyCollection<string> chosen)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(chosen);

        var window = new DossierShaftPickerWindow(project, chosen)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true
            ? window._all.Where(c => c.IsChosen).Select(c => c.ShaftNumber).ToList()
            : null;
    }

    private bool Matches(object item)
    {
        if (item is not DossierShaftChoice choice)
            return false;

        var search = SearchBox.Text?.Trim() ?? "";
        if (search.Length == 0)
            return true;

        return choice.ShaftNumber.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || choice.Detail.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || choice.Funktion.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
        UpdateCount();
    }

    private void OnGroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_view is null)
            return;

        _view.GroupDescriptions.Clear();

        var index = GroupBox.SelectedIndex;
        if (index <= 0)
            return;

        // Ohne Wert eine eigene Gruppe: sonst verschwinden Schaechte ohne
        // Strasse oder Funktion optisch in einer namenlosen Sammelgruppe.
        foreach (var choice in _all)
        {
            var value = index == 1 ? choice.Street : choice.Funktion;
            choice.GroupLabel = string.IsNullOrWhiteSpace(value)
                ? (index == 1 ? "(ohne Strasse)" : "(ohne Funktion)")
                : value;
        }

        _view.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(DossierShaftChoice.GroupLabel)));
    }

    private void OnChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DossierShaftChoice.IsChosen))
            UpdateCount();
    }

    private void OnSelectVisible(object sender, RoutedEventArgs e)
    {
        foreach (var item in _view.Cast<DossierShaftChoice>().ToList())
            item.IsChosen = true;

        UpdateCount();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        foreach (var choice in _all)
            choice.IsChosen = false;

        UpdateCount();
    }

    private void UpdateCount()
    {
        var chosen = _all.Count(c => c.IsChosen);
        CountText.Text = chosen == 1
            ? $"1 von {_all.Count} Schächten gewählt"
            : $"{chosen} von {_all.Count} Schächten gewählt";
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
