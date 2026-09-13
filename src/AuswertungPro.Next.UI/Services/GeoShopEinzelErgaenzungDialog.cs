using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

/// <summary>«Fehlende Felder aus GeoShop-XTF» an der offenen Haltung oder dem offenen Schacht (11.09.2026):
/// gemerkte Datei, ein kurzer Vorschautext mit Ja/Nein, dann dieselbe Uebernahme wie der grosse Abgleich.
/// Die fachlichen Regeln liegen in <see cref="GeoShopEinzelErgaenzung"/>; hier nur Datei, Dialoge und Thread.</summary>
public sealed class GeoShopEinzelErgaenzungDialog(IGeoShopLeser leser, IDialogService dialogs, AppSettings settings)
{
    public const string Filter = "GeoShop-XTF (*.xtf)|*.xtf";

    /// <summary>Die gemerkte XTF, sonst Dateiwahl. Null, wenn keine Datei gewaehlt wurde.</summary>
    public string? Datei(bool neuWaehlen = false)
    {
        var pfad = settings.GeoShopXtfPath;
        if (!neuWaehlen && !string.IsNullOrWhiteSpace(pfad) && File.Exists(pfad)) return pfad;
        var gewaehlt = dialogs.OpenFile("GeoShop-XTF wählen", Filter,
            string.IsNullOrWhiteSpace(pfad) ? null : Path.GetDirectoryName(pfad));
        if (string.IsNullOrWhiteSpace(gewaehlt)) return null;
        Merke(gewaehlt);
        return gewaehlt;
    }

    public void Merke(string datei)
    {
        if (settings.GeoShopXtfPath == datei) return;
        settings.GeoShopXtfPath = datei;
        settings.Save();
    }

    /// <summary>Liest im Hintergrund, fragt, schreibt. True, wenn etwas uebernommen wurde.</summary>
    public async Task<bool> ErgaenzeAsync(Project projekt, Guid id, string art, Func<bool> darfSchreiben)
    {
        if (!darfSchreiben()) return false;
        var datei = Datei();
        if (datei is null) return false;
        var ziel = Ziel(projekt, id, art);
        if (ziel is null) return false;
        GeoShopEinzelErgaenzung.Ergebnis ergebnis;
        try { ergebnis = await Task.Run(() => GeoShopEinzelErgaenzung.Plane(ziel, leser, datei)); }
        catch (Exception ex)
        {
            dialogs.Error($"Die GeoShop-XTF konnte nicht gelesen werden.\n\n{ex.Message}", "GeoShop-XTF");
            return false;
        }
        if (!ergebnis.HatAenderungen) { dialogs.Info(ergebnis.Text, "GeoShop-XTF"); return false; }
        if (!dialogs.Confirm(ergebnis.Text, "Fehlende Felder aus GeoShop-XTF übernehmen?")) return false;
        if (!darfSchreiben()) return false;
        // Der Datensatz wird frisch aufgeloest; der Anwender prueft Instanz und Stand gegen den Plan.
        var aktuell = Ziel(projekt, id, art);
        if (aktuell is null) return false;
        try { return GeoShopEinzelErgaenzung.WendeAn(ergebnis, aktuell) > 0; }
        catch (InvalidOperationException ex) { dialogs.Error(ex.Message, "GeoShop-XTF"); return false; }
    }

    private static GeoShopZiel? Ziel(Project projekt, Guid id, string art) => art == "haltung"
        ? projekt.Data.FirstOrDefault(r => r.Id == id) is { } h ? GeoShopZiel.Fuer(h, projekt) : null
        : projekt.SchaechteData.FirstOrDefault(r => r.Id == id) is { } s ? GeoShopZiel.Fuer(s, projekt) : null;
}
