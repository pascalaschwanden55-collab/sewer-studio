using System;
using System.Collections.Specialized;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Nova-Etappe 2: Brotkrume, Aufgaben-Chip und Speicherstand der Kopf-/Fusszeile.</summary>
public partial class ShellViewModel
{
    private DateTime? _letzteSpeicherungLokal;

    public string Brotkrume => ShellNovaKopfzeile.Brotkrume(IsProjectReady ? Project.Name : null, SelectedNavItem?.Title);
    public HaltungRecord? NaechsteAufgabe { get; private set; }
    public string NaechsteAufgabeText => IsProjectReady ? NaechsteAufgabeRegel.ChipText(NaechsteAufgabe) : string.Empty;
    public string SpeicherstandText => ShellNovaKopfzeile.Speicherstand(IsProjectReady ? Project.Name : null, _letzteSpeicherungLokal, IsProjectReady && Project.Dirty);
    public IRelayCommand NaechsteAufgabePruefenCommand { get; private set; } = null!;

    private void InitNova()
    {
        NaechsteAufgabePruefenCommand = new RelayCommand(NaechsteAufgabePruefen, () => NaechsteAufgabe is not null);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedNavItem) or nameof(IsProjectReady) or nameof(Project))
                AktualisiereNovaKopfzeile();
        };
    }

    /// <summary>Nach Projektwechsel, Speichern, Listenaenderung: alle drei Texte neu.</summary>
    public void AktualisiereNovaKopfzeile()
    {
        NaechsteAufgabe = IsProjectReady ? NaechsteAufgabeRegel.Naechste(Project.Data) : null;
        OnPropertyChanged(nameof(Brotkrume));
        OnPropertyChanged(nameof(NaechsteAufgabe));
        OnPropertyChanged(nameof(NaechsteAufgabeText));
        OnPropertyChanged(nameof(SpeicherstandText));
        NaechsteAufgabePruefenCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>Erfolgreiches Speichern merken (Fusszeile "gespeichert HH:mm").</summary>
    private void MerkeSpeicherung()
    {
        _letzteSpeicherungLokal = DateTime.Now;
        OnPropertyChanged(nameof(SpeicherstandText));
    }

    /// <summary>Chip/Knopf: Haltung oeffnen und ihr Video pruefen (Inventar 8.1).</summary>
    private void NaechsteAufgabePruefen()
    {
        var record = NaechsteAufgabe;
        if (record is null)
            return;
        NavigateToHolding(record);
        if (CurrentPage is Pages.DataPageViewModel dataPage && dataPage.PlayVideoCommand.CanExecute(record))
            dataPage.PlayVideoCommand.Execute(record);
    }

    private void BeobachteHaltungsliste(Project p)
        => p.Data.CollectionChanged += (_, _) => AktualisiereNovaKopfzeile();
}
