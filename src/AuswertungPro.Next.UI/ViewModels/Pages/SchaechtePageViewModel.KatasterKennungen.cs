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
    // Haengt wie "Leere Felder aus QGIS" an CanMutateShaftData — derselben Schranke
    // wie die uebrigen aendernden Aktionen der Seite.
    [RelayCommand]
    private void KatasterKennungenErgaenzen()
    {
        if (_geoShop is null || !CanMutateShaftData)
            return;

        var anzahl = new GeoShopAbgleichDialog(_geoShop, _dialogs, datei => { Settings.GeoShopXtfPath = datei; Settings.Save(); }).Zeige(BauteilArt.Schacht,
            () => Records.Select(r => GeoShopZiel.Fuer(r, _shell.Project)).ToArray(), () => CanMutateShaftData);
        if (anzahl > 0) _dialogs.Info($"GeoShop: {anzahl} Schächte abgeglichen. Bitte das Projekt speichern.", "GeoShop-Abgleich");
    }
}
