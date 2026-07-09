using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>Eine gewaehlte Massnahme fuer EINEN Schacht: Name + Menge + Preis (pro Schacht anpassbar).</summary>
public sealed partial class SchachtMassnahmePositionVm : ObservableObject
{
    private readonly Action _changed;

    public string Name { get; }

    [ObservableProperty] private decimal _menge;
    [ObservableProperty] private decimal _preis;

    public decimal ZeilenTotal => Menge * Preis;

    public SchachtMassnahmePositionVm(string name, decimal menge, decimal preis, Action changed)
    {
        Name = name;
        _menge = menge;
        _preis = preis;
        _changed = changed;
    }

    partial void OnMengeChanged(decimal value) => NotifyChanged();
    partial void OnPreisChanged(decimal value) => NotifyChanged();

    private void NotifyChanged()
    {
        OnPropertyChanged(nameof(ZeilenTotal));
        _changed();
    }
}

/// <summary>
/// ViewModel des Schacht-Sanierungsmassnahmen-Fensters (einfacher, NPK-freier Weg):
/// klickbare Katalog-Liste -> gewaehlte Positionen (Menge/Preis pro Schacht editierbar)
/// -> "Uebernehmen" schreibt Massnahmen-Text + Summe in den Schacht-Record und meldet die
/// Auswahl zum Speichern. Reine, testbare Logik — kein Fenster-, kein Datei-Bezug.
/// </summary>
public sealed partial class SchachtMassnahmenViewModel : ObservableObject
{
    private const string MeasureId = "SCHACHT_EMPFEHLUNG";
    private const string MeasureName = "Empfohlene Massnahmen";

    private readonly SchachtRecord _record;
    private readonly Action<HoldingCost> _onUebernehmen;
    private readonly Func<IReadOnlyList<SchachtMassnahmeKatalogEintrag>?>? _onListeBearbeiten;

    public string SchachtNummer { get; }
    public string Funktion { get; }
    public string Zustandsklasse { get; }

    public string Titel => string.IsNullOrWhiteSpace(SchachtNummer)
        ? "Sanierungsmassnahmen"
        : $"Sanierungsmassnahmen — Schacht {SchachtNummer}";

    public string Kontext => string.Join("   ", new[]
    {
        string.IsNullOrWhiteSpace(Funktion) ? null : $"Funktion: {Funktion}",
        string.IsNullOrWhiteSpace(Zustandsklasse) ? null : $"Zustandsklasse: {Zustandsklasse}",
    }.Where(s => s is not null));

    /// <summary>Die selbst gepflegte, klickbare Massnahmen-Liste (Name + Preis).</summary>
    public ObservableCollection<SchachtMassnahmeKatalogEintrag> Katalog { get; }

    /// <summary>Die fuer diesen Schacht gewaehlten Positionen.</summary>
    public ObservableCollection<SchachtMassnahmePositionVm> Positionen { get; } = new();

    [ObservableProperty] private decimal _total;

    public string TotalText => $"{Total.ToString("N2", SwissCulture)} CHF";

    private static readonly CultureInfo SwissCulture = CultureInfo.GetCultureInfo("de-CH");

    /// <summary>Wird ausgeloest, wenn das Fenster geschlossen werden soll (Uebernehmen/Abbrechen).</summary>
    public event Action? CloseRequested;

    public SchachtMassnahmenViewModel(
        SchachtRecord record,
        IEnumerable<SchachtMassnahmeKatalogEintrag> katalog,
        HoldingCost? bestehend,
        Action<HoldingCost> onUebernehmen,
        Func<IReadOnlyList<SchachtMassnahmeKatalogEintrag>?>? onListeBearbeiten = null)
    {
        _record = record ?? throw new ArgumentNullException(nameof(record));
        _onUebernehmen = onUebernehmen ?? throw new ArgumentNullException(nameof(onUebernehmen));
        _onListeBearbeiten = onListeBearbeiten;

        Katalog = new ObservableCollection<SchachtMassnahmeKatalogEintrag>(
            (katalog ?? Enumerable.Empty<SchachtMassnahmeKatalogEintrag>()).Where(e => e is not null));

        SchachtNummer = ResolveSchachtNummer(record);
        Funktion = record.GetFieldValue("Funktion");
        Zustandsklasse = record.GetFieldValue("Zustandsklasse");

        if (bestehend is not null)
        {
            foreach (var line in bestehend.Measures
                         .SelectMany(m => m.Lines)
                         .Where(l => l.Selected && !string.IsNullOrWhiteSpace(l.Text)))
            {
                Positionen.Add(new SchachtMassnahmePositionVm(
                    line.Text.Trim(), line.Qty <= 0m ? 1m : line.Qty, line.UnitPrice, Recalc));
            }
        }

        Recalc();
    }

    [RelayCommand]
    private void Hinzufuegen(SchachtMassnahmeKatalogEintrag? eintrag)
    {
        if (eintrag is null || string.IsNullOrWhiteSpace(eintrag.Name))
            return;

        var name = eintrag.Name.Trim();
        var vorhanden = Positionen.FirstOrDefault(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (vorhanden is not null)
        {
            vorhanden.Menge += 1m; // loest Recalc via OnMengeChanged aus
            return;
        }

        Positionen.Add(new SchachtMassnahmePositionVm(name, 1m, eintrag.Preis, Recalc));
        Recalc();
    }

    [RelayCommand]
    private void Entfernen(SchachtMassnahmePositionVm? position)
    {
        if (position is null)
            return;
        if (Positionen.Remove(position))
            Recalc();
    }

    [RelayCommand]
    private void Uebernehmen()
    {
        var cost = BuildCost();
        ApplyAbdeckungStkDefault();

        if (Positionen.Count == 0)
            SchachtEmpfehlungRecordMapper.Clear(_record);
        else
            SchachtEmpfehlungRecordMapper.ApplyTo(_record, cost);

        _onUebernehmen(cost);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Abbrechen() => CloseRequested?.Invoke();

    [RelayCommand]
    private void ListeBearbeiten()
    {
        var result = _onListeBearbeiten?.Invoke();
        if (result is null)
            return;

        Katalog.Clear();
        foreach (var e in result.Where(e => e is not null))
            Katalog.Add(e);
    }

    private HoldingCost BuildCost()
    {
        var measure = new MeasureCost { MeasureId = MeasureId, MeasureName = MeasureName };
        foreach (var p in Positionen)
            measure.Lines.Add(new CostLine { Text = p.Name, Qty = p.Menge, UnitPrice = p.Preis, Selected = true });
        measure.Total = Positionen.Sum(p => p.ZeilenTotal);

        return new HoldingCost { Holding = SchachtNummer, Measures = { measure }, Total = measure.Total };
    }

    private void ApplyAbdeckungStkDefault()
    {
        foreach (var position in Positionen)
            SchachtAbdeckungStkAutoFill.TryApplyForMeasure(_record, null, position.Name);
    }

    private void Recalc()
    {
        Total = Positionen.Sum(p => p.ZeilenTotal);
        OnPropertyChanged(nameof(TotalText));
    }

    private static string ResolveSchachtNummer(SchachtRecord record)
    {
        var byName = record.GetFieldValue("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(byName))
            return byName.Trim();

        var byNr = record.GetFieldValue("Nr.");
        return string.IsNullOrWhiteSpace(byNr) ? "" : byNr.Trim();
    }
}
