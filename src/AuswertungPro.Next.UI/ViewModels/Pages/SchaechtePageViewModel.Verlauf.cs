using AuswertungPro.Next.Application.UseCases.Datenaenderungen;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Optik Aufgabe 16: Rueckgaengig/Wiederholen der Schachtdaten — Gegenstueck zu
/// <see cref="DataPageViewModel.WendeVerlauf"/>. Uebernahmen (GeoShop, WebGIS, QGIS) leeren den Verlauf.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    internal IDatenaenderungsVerlauf Verlauf => _shell.DatenVerlauf;

    internal void WendeVerlauf(bool rueckgaengig)
    {
        if (!_shell.IsProjectReady || !CanMutateShaftData)
            return;

        var ergebnis = rueckgaengig
            ? Verlauf.Rueckgaengig(DatenaenderungsBereich.Schaechte)
            : Verlauf.Wiederholen(DatenaenderungsBereich.Schaechte);
        if (!ergebnis.Angewendet)
        {
            _toasts?.Warning(ergebnis.Meldung);
            // Schlusswelle (Item 4): "nicht vollständig" (Teilweise=true) - Feldwerte koennen an den
            // betroffenen Datensaetzen stehen geblieben sein, obwohl der Schritt selbst nicht als
            // Rueckgaengig-Eintrag zaehlt. Siehe DataPageViewModel.WendeVerlauf fuer denselben Fall.
            if (ergebnis.Teilweise)
            {
                _shell.MarkProjectDirty();
                ScheduleAutoSave();
                FelderExternErgaenzt?.Invoke();
            }
            return;
        }

        _shell.MarkProjectDirty();
        ScheduleAutoSave();
        FelderExternErgaenzt?.Invoke();
        LastResult = ergebnis.Meldung;
    }

    /// <summary>Eine Uebernahme hat Feldwerte geschrieben: derselbe Abschluss wie bei den Haltungen.</summary>
    private void MeldeUebernahme() => SeitenUebernahme.Abschliessen(_shell, ScheduleAutoSave, FelderExternErgaenzt);
}
