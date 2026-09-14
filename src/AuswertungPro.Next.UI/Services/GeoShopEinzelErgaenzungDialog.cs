using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

/// <summary>«Fehlende Felder aus GeoShop-XTF» an der offenen Haltung oder dem offenen Schacht (11.09.2026):
/// gemerkte Datei, derselbe Feldvergleich samt Sicherung wie der grosse Abgleich.
/// Die fachlichen Regeln liegen im gemeinsamen Application-Planer; hier nur Datei und Dialoganbindung.</summary>
public sealed class GeoShopEinzelErgaenzungDialog(IGeoShopLeser leser, IDialogService dialogs, AppSettings settings, IGeoShopSicherung? sicherung = null)
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
    public Task<bool> ErgaenzeAsync(Project projekt, Guid id, string art, Func<bool> darfSchreiben)
    {
        if (!darfSchreiben()) return Task.FromResult(false);
        var datei = Datei();
        if (datei is null || Ziel(projekt, id, art) is null) return Task.FromResult(false);
        var dialog = new GeoShopAbgleichDialog(leser, dialogs, Merke, sicherung);
        var anzahl = dialog.ZeigeDatei(art == "haltung" ? BauteilArt.Haltung : BauteilArt.Schacht,
            () => Ziel(projekt, id, art) is { } ziel ? [ziel] : [], darfSchreiben, datei);
        return Task.FromResult(anzahl > 0);
    }

    private static GeoShopZiel? Ziel(Project projekt, Guid id, string art) => art == "haltung"
        ? projekt.Data.FirstOrDefault(r => r.Id == id) is { } h ? GeoShopZiel.Fuer(h, projekt) : null
        : projekt.SchaechteData.FirstOrDefault(r => r.Id == id) is { } s ? GeoShopZiel.Fuer(s, projekt) : null;
}
