using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>Eine bearbeitbare Zeile der globalen Schacht-Massnahmen-Liste.</summary>
public sealed partial class SchachtMassnahmeKatalogRowVm : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private decimal _preis;
    [ObservableProperty] private string _einheit;

    public SchachtMassnahmeKatalogRowVm(string name, decimal preis, string einheit)
    {
        _name = name;
        _preis = preis;
        _einheit = string.IsNullOrWhiteSpace(einheit) ? "Stk" : einheit;
    }
}

/// <summary>
/// Editor der globalen, selbst gepflegten Schacht-Massnahmen-Liste: Name + Preis (+ Einheit)
/// pro Zeile hinzufuegen/loeschen/aendern. "Speichern" legt die bereinigte Liste in
/// <see cref="Ergebnis"/> ab; das Persistieren (Store) uebernimmt der Aufrufer.
/// Reine, testbare Logik.
/// </summary>
public sealed partial class SchachtMassnahmenKatalogEditorViewModel : ObservableObject
{
    public ObservableCollection<SchachtMassnahmeKatalogRowVm> Zeilen { get; } = new();

    [ObservableProperty] private SchachtMassnahmeKatalogRowVm? _selected;

    /// <summary>Nach "Speichern" die bereinigte Liste (leere Namen entfernt).</summary>
    public IReadOnlyList<SchachtMassnahmeKatalogEintrag> Ergebnis { get; private set; }
        = Array.Empty<SchachtMassnahmeKatalogEintrag>();

    /// <summary>true = gespeichert, false = abgebrochen.</summary>
    public event Action<bool>? CloseRequested;

    public SchachtMassnahmenKatalogEditorViewModel(IEnumerable<SchachtMassnahmeKatalogEintrag> eintraege)
    {
        foreach (var e in (eintraege ?? Enumerable.Empty<SchachtMassnahmeKatalogEintrag>()).Where(e => e is not null))
            Zeilen.Add(new SchachtMassnahmeKatalogRowVm(e.Name, e.Preis, e.Einheit));
    }

    [RelayCommand]
    private void Hinzufuegen()
    {
        var row = new SchachtMassnahmeKatalogRowVm("", 0m, "Stk");
        Zeilen.Add(row);
        Selected = row;
    }

    [RelayCommand]
    private void Entfernen(SchachtMassnahmeKatalogRowVm? row)
    {
        row ??= Selected;
        if (row is not null)
            Zeilen.Remove(row);
    }

    [RelayCommand]
    private void Speichern()
    {
        Ergebnis = Zeilen
            .Where(z => !string.IsNullOrWhiteSpace(z.Name))
            .Select(z => new SchachtMassnahmeKatalogEintrag
            {
                Name = z.Name.Trim(),
                Preis = z.Preis,
                Einheit = string.IsNullOrWhiteSpace(z.Einheit) ? "Stk" : z.Einheit.Trim(),
            })
            .ToList();

        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Abbrechen() => CloseRequested?.Invoke(false);
}
