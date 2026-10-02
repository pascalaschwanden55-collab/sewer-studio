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

        SeitenVerlauf.Wende(Verlauf, DatenaenderungsBereich.Schaechte, rueckgaengig, meldung => _toasts?.Warning(meldung),
            () => { _shell.MarkProjectDirty(); ScheduleAutoSave(); FelderExternErgaenzt?.Invoke(); },
            meldung => LastResult = meldung);
    }

    /// <summary>Eine Uebernahme hat Feldwerte geschrieben: derselbe Abschluss wie bei den Haltungen.</summary>
    private void MeldeUebernahme() => SeitenUebernahme.Abschliessen(_shell, ScheduleAutoSave, FelderExternErgaenzt);
}
