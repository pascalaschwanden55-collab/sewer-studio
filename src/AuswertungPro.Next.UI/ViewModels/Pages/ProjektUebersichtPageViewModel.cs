using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed record ZustandZeile(string Klasse, string Label, int Anzahl);
public sealed record SchadenZeile(string Hauptcode, string Klartext, int Anzahl, double Anteil);
public sealed record KiLaufZeile(string Haltung, string Badge, string Meta);
public sealed record ProjektZeile(string Name, string Pfad, string Meta);

/// <summary>Nova-Etappe 2, Inventar 4.1: Uebersicht des offenen Projekts. Alle Zahlen aus einem Bestand.</summary>
public sealed partial class ProjektUebersichtPageViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo DeCh = CultureInfo.GetCultureInfo("de-CH");

    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;
    private readonly ICodingSuggestionRegistry _register;

    [ObservableProperty] private string _heroTitel = string.Empty;
    [ObservableProperty] private string _heroText = string.Empty;
    [ObservableProperty] private ProjektUebersichtKennzahlen? _kennzahlen;
    [ObservableProperty] private DashboardStatistics? _statistik;
    [ObservableProperty] private string _sanierungskostenText = "0";
    public ObservableCollection<KiLaufZeile> KiLaeufe { get; } = new();
    public ObservableCollection<ZustandZeile> ZustandLegende { get; } = new();
    public ObservableCollection<SchadenZeile> Schaeden { get; } = new();
    public ObservableCollection<ProjektZeile> LetzteProjekte { get; } = new();
    public IRelayCommand NaechsteHaltungPruefenCommand => _shell.NaechsteAufgabePruefenCommand;
    public string NaechsteAufgabeText => _shell.NaechsteAufgabeText;

    public ProjektUebersichtPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;
        _register = sp.CodingSuggestionRegistry;
        _register.Geaendert += OnRegisterGeaendert;
        _shell.Project.Data.CollectionChanged += (_, _) => Aktualisiere();
        Aktualisiere();
    }

    /// <summary>
    /// Marshallt <see cref="ICodingSuggestionRegistry.Geaendert"/> auf den WPF-UI-Thread. Das
    /// Register feuert synchron auf dem scannenden Hintergrund-/Codiermodus-Thread (Task 9);
    /// <see cref="Aktualisiere"/> darf Bindungen aber nur auf dem UI-Thread setzen.
    /// </summary>
    private void OnRegisterGeaendert()
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is not null && !d.CheckAccess())
            d.BeginInvoke(Aktualisiere);
        else
            Aktualisiere();
    }

    private void Aktualisiere()
    {
        var p = _shell.Project;
        HeroTitel = p.Name;

        var kennzahlen = ProjektUebersichtRechner.Berechne(p);
        Kennzahlen = kennzahlen;
        HeroText = ProjektUebersichtRechner.HeroText(kennzahlen);

        var statistik = BaueStatistik(p);
        Statistik = statistik;
        SanierungskostenText = (statistik.HaltungSanierungsKosten + statistik.SchachtSanierungsKosten)
            .ToString("#,##0", DeCh).Replace('’', '\'');

        ZustandLegende.Clear();
        var anzahlJeKlasse = p.Data
            .GroupBy(r => DashboardStatisticsBuilder.NormalizeZustandsklasse(r.GetFieldValue(FieldKeys.ConditionClass)))
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var z in BaueZustandLegende(anzahlJeKlasse))
            ZustandLegende.Add(z);

        Schaeden.Clear();
        var top = statistik.TopSchaeden;
        var max = top.Count == 0 ? 1 : top.Max(b => b.Count);
        foreach (var b in top)
            Schaeden.Add(new SchadenZeile(b.Key, b.Label, b.Count, (double)b.Count / max));

        KiLaeufe.Clear();
        foreach (var lauf in _register.Heute())
            KiLaeufe.Add(BaueKiLaufZeile(lauf.Haltung, lauf.Set.Suggestions
                .Select(s => (s.Kind.ToString(), s.Meter is { } m && !s.MeterIsEstimated
                    ? $"Meter {m.ToString("0.00", DeCh)}"
                    : $"Sekunde {s.PeakTimeSeconds:0}"))
                .ToList()));

        LetzteProjekte.Clear();
        foreach (var pfad in (_sp.Settings.RecentProjectPaths ?? new List<string>()).Take(3))
            LetzteProjekte.Add(new ProjektZeile(
                Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(pfad)) ?? pfad),
                pfad,
                string.Empty));

        OnPropertyChanged(nameof(NaechsteAufgabeText));
    }

    /// <summary>
    /// Derselbe Kosten-Ladeweg wie <c>OverviewPageViewModel.BuildStatsFor</c>: Schachtkosten aus
    /// Matrix UND Massnahmen-Dialog, die Matrix hat Vorrang (<see cref="SchachtCostStoreMerger"/>).
    /// </summary>
    private DashboardStatistics BaueStatistik(Project p)
    {
        var pfad = _sp.Settings.LastProjectPath;
        var hCosts = LadeKostenSpeicher(_sp.CostStores.CreateProjectCostStore(), pfad);
        var matrix = LadeKostenSpeicher(_sp.CostStores.CreateProjectCostStore("schacht_costs.json"), pfad);
        var empfehlungen = LadeKostenSpeicher(_sp.CostStores.CreateProjectCostStore("schacht_empfehlungen.json"), pfad);
        var sCosts = SchachtCostStoreMerger.Merge(matrix, empfehlungen);
        return DashboardStatisticsBuilder.Build(p, hCosts, sCosts);
    }

    private static ProjectCostStore LadeKostenSpeicher(IProjectCostStoreRepository repo, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return new ProjectCostStore();

        var store = repo.Load(projectPath, out var error);
        return error is null ? store : new ProjectCostStore();
    }

    internal static IReadOnlyList<ZustandZeile> BaueZustandLegende(IReadOnlyDictionary<string, int> anzahlJeKlasse)
    {
        (string Klasse, string Label)[] reihenfolge =
        [
            ("4", "Z4 · kein Handlungsbedarf"), ("3", "Z3 · langfristig"), ("2", "Z2 · mittelfristig"),
            ("1", "Z1 · kurzfristig"), ("0", "Z0 · sofort"), ("", "nicht berechnet")
        ];
        return reihenfolge
            .Select(r => new ZustandZeile(r.Klasse, r.Label, anzahlJeKlasse.TryGetValue(r.Klasse, out var n) ? n : 0))
            .ToList();
    }

    internal static KiLaufZeile BaueKiLaufZeile(string haltung, IReadOnlyList<(string Art, string Ort)> vorschlaege)
    {
        var arten = vorschlaege.Select(v => v.Art).Distinct().ToList();
        var orte = vorschlaege.Select(v => v.Ort).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToList();
        return new KiLaufZeile(
            haltung,
            string.Join(" · ", arten),
            orte.Count == 0 ? haltung : $"{haltung} · {string.Join(", ", orte)}");
    }

    [RelayCommand]
    private void HaltungenOeffnen() => _shell.NavigateTo("Haltungen");

    [RelayCommand]
    private void ZustandFilter(string? klasse)
        => _shell.NavigateToDataPage(new DataPageStartFilter(FieldKeys.ConditionClass, string.IsNullOrEmpty(klasse) ? "ohne" : klasse));

    [RelayCommand]
    private void KiLaufPruefen(string? haltung)
    {
        var record = _shell.Project.Data.FirstOrDefault(r => string.Equals(r.GetFieldValue(FieldKeys.HoldingName), haltung, StringComparison.OrdinalIgnoreCase));
        if (record is null)
            return;

        _shell.NavigateToHolding(record);
        if (_shell.CurrentPage is DataPageViewModel dp && dp.PlayVideoCommand.CanExecute(record))
            dp.PlayVideoCommand.Execute(record);
    }

    [RelayCommand]
    private void ProjektOeffnen(string? pfad)
    {
        if (!string.IsNullOrWhiteSpace(pfad))
            _shell.TryOpenProject(pfad);
    }

    public void Dispose() => _register.Geaendert -= OnRegisterGeaendert;
}
