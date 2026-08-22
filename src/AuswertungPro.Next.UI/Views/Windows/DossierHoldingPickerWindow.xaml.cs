using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;

using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Eine waehlbare Haltung in der Auswahlliste.</summary>
public sealed partial class DossierHoldingChoice : ObservableObject
{
    public DossierHoldingChoice(HaltungRecord record, bool chosen)
    {
        Id = record.Id;
        HoldingName = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        if (HoldingName.Length == 0)
            HoldingName = "(ohne Name)";

        Street = (record.GetFieldValue(FieldKeys.Street) ?? "").Trim();
        Owner = (record.GetFieldValue(FieldKeys.Owner) ?? "").Trim();

        var measures = (record.GetFieldValue(
            FieldKeys.RecommendedRehabilitationMeasures) ?? "").Trim();

        Detail = string.Join(" · ", new[] { Street, Owner, measures }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        var length = record.GetFieldValue(FieldKeys.HoldingLengthMeters);
        Length = FormatLength(length);

        Condition = DescribeCondition(
            DashboardStatisticsBuilder.NormalizeZustandsklasse(
                record.GetFieldValue(FieldKeys.ConditionClass)));

        _isChosen = chosen;
    }

    public Guid Id { get; }
    public string HoldingName { get; }
    public string Street { get; }
    public string Owner { get; }
    public string Detail { get; }
    public string Length { get; }
    public string Condition { get; }

    /// <summary>Gruppenueberschrift, je nach gewaehlter Gruppierung.</summary>
    public string GroupLabel { get; set; } = "";

    [ObservableProperty]
    private bool _isChosen;

    private static string FormatLength(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "—";

        var normalized = raw.Replace("'", "").Replace(",", ".");
        return double.TryParse(
            normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value > 0
            ? value.ToString("0.0", CultureInfo.GetCultureInfo("de-CH")) + " m"
            : "—";
    }

    private static string DescribeCondition(string normalized) => normalized switch
    {
        "0" => "Z0",
        "1" => "Z1",
        "2" => "Z2",
        "3" => "Z3",
        "4" => "Z4",
        _ => "—"
    };
}

/// <summary>
/// Auswahl der Haltungen einer Liegenschaft. Bei einem ganzen Gebiet sind das
/// schnell 70 Zeilen — deshalb Suchfeld und Gruppierung nach Strasse oder
/// Eigentuemer.
/// </summary>
public partial class DossierHoldingPickerWindow : Window
{
    private readonly List<DossierHoldingChoice> _all;
    private readonly ICollectionView _view;

    private DossierHoldingPickerWindow(Project project, IReadOnlyCollection<Guid> chosen)
    {
        InitializeComponent();

        var chosenSet = new HashSet<Guid>(chosen);
        _all = project.Data
            .Select(record => new DossierHoldingChoice(record, chosenSet.Contains(record.Id)))
            .OrderBy(c => c.HoldingName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var choice in _all)
            choice.PropertyChanged += OnChoiceChanged;

        HoldingList.ItemsSource = _all;
        _view = CollectionViewSource.GetDefaultView(_all);
        _view.Filter = Matches;

        UpdateCount();
    }

    /// <summary>
    /// Zeigt die Auswahl. Liefert die neue Liste oder null bei Abbruch.
    /// </summary>
    public static List<Guid>? ShowFor(Project project, IReadOnlyCollection<Guid> chosen)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(chosen);

        var window = new DossierHoldingPickerWindow(project, chosen)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return window.ShowDialog() == true
            ? window._all.Where(c => c.IsChosen).Select(c => c.Id).ToList()
            : null;
    }

    private bool Matches(object item)
    {
        if (item is not DossierHoldingChoice choice)
            return false;

        var search = SearchBox.Text?.Trim() ?? "";
        if (search.Length == 0)
            return true;

        return choice.HoldingName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || choice.Detail.Contains(search, StringComparison.CurrentCultureIgnoreCase);
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

        // Ohne Wert eine eigene Gruppe: sonst verschwinden Haltungen ohne
        // Strasse oder Eigentuemer optisch in einer namenlosen Sammelgruppe.
        foreach (var choice in _all)
        {
            var value = index == 1 ? choice.Street : choice.Owner;
            choice.GroupLabel = string.IsNullOrWhiteSpace(value)
                ? (index == 1 ? "(ohne Strasse)" : "(ohne Eigentümer)")
                : value;
        }

        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DossierHoldingChoice.GroupLabel)));
    }

    private void OnChoiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DossierHoldingChoice.IsChosen))
            UpdateCount();
    }

    private void OnSelectVisible(object sender, RoutedEventArgs e)
    {
        foreach (var item in _view.Cast<DossierHoldingChoice>().ToList())
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
            ? $"1 von {_all.Count} Leitungen gewählt"
            : $"{chosen} von {_all.Count} Leitungen gewählt";
    }

    private void OnApply(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
