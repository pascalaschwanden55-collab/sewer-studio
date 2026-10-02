using AuswertungPro.Next.Application.UseCases.Datenaenderungen;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Optik Aufgabe 16: Rueckgaengig/Wiederholen der Haltungsdaten. Die Seite erfasst ihre Eingaben
/// ueber <see cref="Verlauf"/>; ausgefuehrt wird von der Shell (Menue, Strg+Z/Strg+Y). Danach laufen
/// dieselben Wege wie nach einer Feldaenderung: Anzeige neu, Projekt geaendert, automatisch speichern.
/// </summary>
public sealed partial class DataPageViewModel
{
    internal IDatenaenderungsVerlauf Verlauf => _shell.DatenVerlauf;

    /// <summary>Fuer das abgedockte Tabellenfenster: dieselben Tastenbefehle wie im Hauptfenster.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand RueckgaengigTasteCommand => _shell.RueckgaengigTasteCommand;

    public CommunityToolkit.Mvvm.Input.IRelayCommand WiederholenTasteCommand => _shell.WiederholenTasteCommand;

    internal void WendeVerlauf(bool rueckgaengig)
    {
        if (!_shell.IsProjectReady)
            return;

        // Der Ablauf (auch der Fall «nicht vollständig») steht einmal fuer beide Seiten in SeitenVerlauf.
        SeitenVerlauf.Wende(Verlauf, DatenaenderungsBereich.Haltungen, rueckgaengig, meldung => _toasts.Warning(meldung),
            () => { _shell.MarkProjectDirty(); ScheduleAutoSave(); FelderExternErgaenzt?.Invoke(); },
            ShowSaveStatus);
    }
}
