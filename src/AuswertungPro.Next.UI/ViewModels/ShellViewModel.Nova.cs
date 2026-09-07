using System;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
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

    /// <summary>KI-Bereitschaft im Leisten-Aufklapper (Task 7, BEWERTUNG N10).</summary>
    public string KiBereitschaftText => KiBereitschaftRegel.Text(KiBereitschaftRegel.Bestimme(AiRuntimeStatusTracker.Current));
    public bool IstKiBereit => KiBereitschaftRegel.Bestimme(AiRuntimeStatusTracker.Current) == KiBereitschaft.Bereit;

    /// <summary>Nova-Etappe 2: Hintergrund-Engine (Leitungsnetz), MainWindow bindet
    /// <c>NetzHintergrund.IsEngineEnabled</c> darauf. Das Control kennt AppSettings nicht selbst;
    /// die Einstellungen-Seite meldet eine Aenderung ueber <see cref="MotionSettings.EngineChanged"/>.</summary>
    public bool HintergrundEngine => _sp.Settings.HintergrundEngine;

    /// <summary>
    /// Nova-Fixwelle F3: Menue "Ansicht &#8594; Klassische Uebersicht". Bei offenem Projekt zeigt
    /// "Uebersicht" sonst die neue Projektuebersicht; die klassische Seite bleibt darueber
    /// erreichbar — sie traegt Projektliste, Vorschau und die Vorschau-PDF. Muster wie der
    /// Umschalter "Alte Haltungsansicht".
    /// </summary>
    public bool KlassischeUebersicht
    {
        get => !_sp.Settings.ShowUebersichtNovaLayout;
        set
        {
            if (value == KlassischeUebersicht)
                return;

            _sp.Settings.ShowUebersichtNovaLayout = !value;
            _sp.Settings.Save();
            OnPropertyChanged();

            // Steht die Uebersicht gerade offen, sofort mit der anderen Seite neu aufbauen.
            if (SelectedNavItem is { } aktiv && string.Equals(aktiv.Title, "Uebersicht", StringComparison.OrdinalIgnoreCase))
                SetCurrentPage(aktiv.CreatePage());
        }
    }

    /// <summary>Globale Suche der Kopfzeile (Strg+K, Inventar 8.5).</summary>
    public GlobaleSucheViewModel GlobaleSuche { get; private set; } = null!;
    public IRelayCommand GlobaleSucheFokusCommand { get; private set; } = null!;

    /// <summary>Wird ausgeloest, wenn Strg+K gedrueckt wurde; das Fenster fokussiert das Suchfeld.</summary>
    public event Action? GlobaleSucheFokusAngefordert;

    private void InitNova()
    {
        NaechsteAufgabePruefenCommand = new RelayCommand(NaechsteAufgabePruefen, () => NaechsteAufgabe is not null);
        GlobaleSuche = new GlobaleSucheViewModel(this);
        GlobaleSucheFokusCommand = new RelayCommand(() => GlobaleSucheFokusAngefordert?.Invoke());
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedNavItem) or nameof(IsProjectReady) or nameof(Project))
                AktualisiereNovaKopfzeile();
        };
        MotionSettings.EngineChanged += OnHintergrundEngineGeaendert;
    }

    /// <summary>Die Einstellungen-Seite hat die Engine umgeschaltet; die Shell-Eigenschaft neu melden.</summary>
    private void OnHintergrundEngineGeaendert() => OnPropertyChanged(nameof(HintergrundEngine));

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
