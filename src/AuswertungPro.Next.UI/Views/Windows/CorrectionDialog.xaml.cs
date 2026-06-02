using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Dialog zur Auswahl des korrekten VSA-Codes aus dem Katalog (kein Freitext).
/// </summary>
public partial class CorrectionDialog : Window
{
    /// <summary>Vom Reviewer ausgewaehlter Code (nur gesetzt wenn DialogResult == true).</summary>
    public string? SelectedCode { get; private set; }

    // Alle Eintraege (unveraendert) und gefilterter CollectionView
    private readonly ObservableCollection<CodeEntry> _allEntries = new();
    private readonly ICollectionView _filteredView;

    public CorrectionDialog()
    {
        InitializeComponent();

        // Katalog-Eintraege aus VsaCodeResolver laden
        var catalog = VsaCodeResolver.CurrentCatalog;
        var codes = catalog?.AllowedCodes() ?? Array.Empty<string>();
        foreach (var c in codes)
        {
            var label = VsaCodeResolver.LookupLabel(c);
            _allEntries.Add(new CodeEntry
            {
                Code = c,
                Display = label is not null ? $"{c} — {label}" : c
            });
        }

        // CollectionView fuer Filterung konfigurieren
        _filteredView = CollectionViewSource.GetDefaultView(_allEntries);
        _filteredView.Filter = FilterEntry;

        CodeListBox.ItemsSource = _filteredView;
    }

    // ── Filterlogik ─────────────────────────────────────────────────────

    private string _searchText = "";

    private bool FilterEntry(object obj)
    {
        if (obj is not CodeEntry entry) return false;
        if (string.IsNullOrEmpty(_searchText)) return true;
        return entry.Code.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || entry.Display.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text;
        _filteredView.Refresh();
    }

    // ── Listeninteraktion ────────────────────────────────────────────────

    private void CodeListBox_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // OK aktivieren sobald ein Eintrag gewaehlt ist
        BtnOk.IsEnabled = CodeListBox.SelectedItem is not null;
    }

    /// <summary>Doppelklick auf einen Eintrag bestaetigt sofort (wie OK).</summary>
    private void CodeListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CodeListBox.SelectedItem is not null)
            AcceptSelection();
    }

    // ── OK / Abbrechen ───────────────────────────────────────────────────

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        AcceptSelection();
    }

    private void AcceptSelection()
    {
        if (CodeListBox.SelectedItem is CodeEntry entry)
        {
            SelectedCode = entry.Code;
            DialogResult = true;
        }
    }
}

/// <summary>Darstellungs-DTO fuer einen VSA-Katalogeintrag in der Liste.</summary>
public sealed class CodeEntry
{
    /// <summary>Kurzform des VSA-Codes (z.B. "BAB").</summary>
    public string Code { get; init; } = "";

    /// <summary>Anzeige-Text: "CODE — Klartext" oder nur "CODE" wenn kein Klartext.</summary>
    public string Display { get; init; } = "";
}
