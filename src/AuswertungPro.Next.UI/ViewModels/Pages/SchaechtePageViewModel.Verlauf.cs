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
            _toasts?.Info(ergebnis.Meldung);
            return;
        }

        _shell.MarkProjectDirty();
        ScheduleAutoSave();
        FelderExternErgaenzt?.Invoke();
        LastResult = ergebnis.Meldung;
    }

    /// <summary>Eine Uebernahme hat Feldwerte geschrieben: Anzeige neu, Verlauf leeren.</summary>
    private void MeldeUebernahme()
    {
        Verlauf.Leere(DatenaenderungsVerlauf.GrundUebernahme);
        FelderExternErgaenzt?.Invoke();
    }
}
