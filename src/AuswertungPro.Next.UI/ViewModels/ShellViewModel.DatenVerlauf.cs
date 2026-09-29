using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Optik Aufgabe 16: Menue «Bearbeiten ▸ Rückgängig/Wiederholen» und Strg+Z/Strg+Y. Der Verlauf
/// selbst liegt in <see cref="IDatenaenderungsVerlauf"/>; die Shell waehlt nur den Bereich der
/// offenen Seite (Haltungen oder Schaechte) und reicht die Ausfuehrung an deren ViewModel weiter,
/// damit Anzeige und automatisches Speichern ueber die vorhandenen Wege laufen.
/// </summary>
public sealed partial class ShellViewModel
{
    private bool _datenVerlaufVerbunden;
    private IRelayCommand? _rueckgaengigCommand;
    private IRelayCommand? _wiederholenCommand;
    private IRelayCommand? _rueckgaengigTasteCommand;
    private IRelayCommand? _wiederholenTasteCommand;

    internal IDatenaenderungsVerlauf DatenVerlauf => _sp.DatenaenderungsVerlauf;

    /// <summary>Menue: nimmt die letzte Aenderung der offenen Seite zurueck.</summary>
    public IRelayCommand RueckgaengigCommand
        => _rueckgaengigCommand ??= new RelayCommand(() => WendeVerlauf(true), () => KannVerlauf(true));

    public IRelayCommand WiederholenCommand
        => _wiederholenCommand ??= new RelayCommand(() => WendeVerlauf(false), () => KannVerlauf(false));

    /// <summary>Strg+Z: wie das Menue, aber nie, solange ein Textfeld den Fokus hat (dort gilt dessen eigenes Rueckgaengig).</summary>
    public IRelayCommand RueckgaengigTasteCommand
        => _rueckgaengigTasteCommand ??= new RelayCommand(() => WendeVerlauf(true),
            () => KannVerlauf(true) && !DatenVerlaufTasten.TexteingabeHatFokus());

    /// <summary>Strg+Y und Strg+Umschalt+Z.</summary>
    public IRelayCommand WiederholenTasteCommand
        => _wiederholenTasteCommand ??= new RelayCommand(() => WendeVerlauf(false),
            () => KannVerlauf(false) && !DatenVerlaufTasten.TexteingabeHatFokus());

    public string RueckgaengigMenuText => DatenVerlaufTasten.MenuText("Rückgängig",
        AktuellerVerlaufBereich is { } b ? DatenVerlauf.RueckgaengigBeschreibung(b) : null);

    public string WiederholenMenuText => DatenVerlaufTasten.MenuText("Wiederholen",
        AktuellerVerlaufBereich is { } b ? DatenVerlauf.WiederholenBeschreibung(b) : null);

    private DatenaenderungsBereich? AktuellerVerlaufBereich => CurrentPage switch
    {
        Pages.DataPageViewModel => DatenaenderungsBereich.Haltungen,
        Pages.SchaechtePageViewModel => DatenaenderungsBereich.Schaechte,
        _ => null,
    };

    private bool KannVerlauf(bool rueckgaengig)
        => IsProjectReady
           && CanLeaveShellContextFromOperationGuards()
           && AktuellerVerlaufBereich is { } bereich
           && (rueckgaengig ? DatenVerlauf.KannRueckgaengig(bereich) : DatenVerlauf.KannWiederholen(bereich));

    private void WendeVerlauf(bool rueckgaengig)
    {
        if (!KannVerlauf(rueckgaengig))
            return;
        switch (CurrentPage)
        {
            case Pages.DataPageViewModel haltungen:
                haltungen.WendeVerlauf(rueckgaengig);
                break;
            case Pages.SchaechtePageViewModel schaechte:
                schaechte.WendeVerlauf(rueckgaengig);
                break;
        }
    }

    /// <summary>Aus <see cref="ReplaceProject"/>: Ein neues Projekt hat einen leeren Verlauf.</summary>
    private void BindeDatenVerlauf(Project projekt)
    {
        if (!_datenVerlaufVerbunden)
        {
            _datenVerlaufVerbunden = true;
            DatenVerlauf.Geaendert += (_, _) => AktualisiereVerlaufBefehle();
            DatenVerlauf.Geleert += (_, e) => _sp.Toasts.Info($"Rückgängig ist nicht mehr möglich: {e.Grund}.");
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CurrentPage) or nameof(IsProjectReady))
                    AktualisiereVerlaufBefehle();
            };
        }
        DatenVerlauf.Binde(projekt);
    }

    /// <summary>Aus <see cref="NotifyShellOperationCommands"/>: Ein laufender Projektvorgang (Import,
    /// Uebertragung, Laden) leert den Verlauf - danach stimmt kein Eintrag mehr sicher.</summary>
    private void PruefeDatenVerlaufBeiVorgang()
    {
        if (!_datenVerlaufVerbunden)
            return;
        if (!CanLeaveShellContextFromOperationGuards())
            DatenVerlauf.Leere(DatenaenderungsVerlauf.GrundVorgang);
        AktualisiereVerlaufBefehle();
    }

    private void AktualisiereVerlaufBefehle()
    {
        if (DatenVerlaufTasten.AufUiThreadVerschoben(AktualisiereVerlaufBefehle))
            return;
        _rueckgaengigCommand?.NotifyCanExecuteChanged();
        _wiederholenCommand?.NotifyCanExecuteChanged();
        _rueckgaengigTasteCommand?.NotifyCanExecuteChanged();
        _wiederholenTasteCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(RueckgaengigMenuText));
        OnPropertyChanged(nameof(WiederholenMenuText));
    }
}
