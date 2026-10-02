using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// GeoShop-Abgleich fuer Schaechte. Der bisherige Befehlsname bleibt als Bindungsvertrag erhalten.
/// </summary>
public sealed partial class SchaechtePageViewModel
{
    private IGeoShopLeser? _geoShop;
    private IGeoShopSicherung? _geoShopSicherung;
    public event System.Action? FelderExternErgaenzt;
    // Haengt wie "Leere Felder aus QGIS" an CanMutateShaftData — derselben Schranke
    // wie die uebrigen aendernden Aktionen der Seite.
    [RelayCommand]
    private void KatasterKennungenErgaenzen()
    {
        if (_geoShop is null || !CanMutateShaftData)
            return;

        var anzahl = new GeoShopAbgleichDialog(_geoShop, _dialogs, datei => { Settings.GeoShopXtfPath = datei; Settings.Save(); }, _geoShopSicherung).Zeige(BauteilArt.Schacht,
            () => Records.Select(r => GeoShopZiel.Fuer(r, _shell.Project)).ToArray(), () => CanMutateShaftData);
        if (anzahl > 0)
        {
            MeldeUebernahme();
            // ScheduleAutoSave() speichert bereits selbst - kein "Bitte speichern" mehr noetig
            // (gleiche Korrektur wie beim Haltungen-Gegenstueck DataPageViewModel.KatasterKennungen.cs).
            var meldung = $"GeoShop: {anzahl} Schächte abgeglichen.";
            if (_toasts is not null)
                _toasts.Success(meldung);
            else
                _dialogs.Info(meldung, "GeoShop-Abgleich");
        }
    }
}
