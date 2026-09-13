using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// GeoShop-Abgleich fuer Haltungen. Der bisherige Befehlsname bleibt als Bindungsvertrag erhalten.
/// </summary>
public sealed partial class DataPageViewModel
{
    private IGeoShopLeser? _geoShop;

    [RelayCommand]
    private void KatasterKennungenErgaenzen()
    {
        if (!_shell.IsProjectReady || _geoShop is null)
            return;

        var anzahl = new GeoShopAbgleichDialog(_geoShop, _dialogs, datei => { Settings.GeoShopXtfPath = datei; Settings.Save(); }).Zeige(BauteilArt.Haltung,
            () => Records.Select(r => GeoShopZiel.Fuer(r, _shell.Project)).ToArray(), () => _shell.IsProjectReady);
        SaveStatus = anzahl > 0 ? $"GeoShop: {anzahl} Haltungen abgeglichen. Bitte speichern." : "GeoShop: Keine Änderungen übernommen.";
        IsSaveStatusVisible = true;
        if (anzahl > 0)
            MeldeFelderExternErgaenzt();
    }
}
