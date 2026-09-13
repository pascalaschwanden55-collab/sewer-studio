using System;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Services;

public static class ObjektaktenDialog
{
    public static Func<Guid, ObjektakteViewModel?> Fabrik(string art, Func<Project> projekt, AppSettings settings,
        Func<bool> bereit, Action geaendert, Action speichern,
        AuswertungPro.Next.Application.Projects.IObjektaktenPaketService pakete, IDialogService dialogs,
        IObjektaktenListenErgaenzungen? ergaenzungen = null, IGeoShopLeser? geoShop = null)
        => id =>
        {
            if (!bereit()) return null;
            var p = projekt();
            bool DarfSchreiben() => bereit() && ReferenceEquals(p, projekt())
                && (art == "haltung" ? p.Data.Any(r => r.Id == id) : p.SchaechteData.Any(r => r.Id == id));
            if (!DarfSchreiben()) return null;
            var bearbeitung = new ObjektaktenBearbeitung(p, id, art, ergaenzungen);
            bearbeitung.PruefeBestand();
            var paketDialog = new ObjektaktenPaketDialog(pakete, dialogs);
            var geo = geoShop is null ? null : new GeoShopEinzelErgaenzungDialog(geoShop, dialogs, settings);
            ObjektakteViewModel? vm = null;
            vm = new ObjektakteViewModel(bearbeitung, settings, geaendert, DarfSchreiben, speichern,
                () => paketDialog.Exportiere(p), () => paketDialog.Importiere(p, DarfSchreiben),
                feld => ListeBearbeiten(feld, () => vm),
                geo is null ? null : () => geo.ErgaenzeAsync(p, id, art, DarfSchreiben),
                geo is null ? null : () => geo.Datei(neuWaehlen: true));
            return vm;
        };

    public static IRelayCommand Befehl(string art, Func<Project> projekt, Func<Guid?> auswahl, AppSettings settings,
        Func<bool> bereit, Action geaendert, Action speichern,
        AuswertungPro.Next.Application.Projects.IObjektaktenPaketService pakete, IDialogService dialogs,
        IObjektaktenListenErgaenzungen? ergaenzungen = null, IGeoShopLeser? geoShop = null)
        => new RelayCommand(() =>
        {
            if (!bereit() || auswahl() is not { } id) return;
            var p = projekt();
            Zeige(p, id, art, settings, geaendert, () => bereit() && ReferenceEquals(p, projekt())
                && (art == "haltung" ? p.Data.Any(r => r.Id == id) : p.SchaechteData.Any(r => r.Id == id)), speichern,
                pakete, dialogs, ergaenzungen, geoShop);
        });

    public static void Zeige(Project projekt, Guid id, string art, AppSettings settings, Action geaendert,
        Func<bool> darfSchreiben, Action speichern,
        AuswertungPro.Next.Application.Projects.IObjektaktenPaketService? pakete = null, IDialogService? dialogs = null,
        IObjektaktenListenErgaenzungen? ergaenzungen = null, IGeoShopLeser? geoShop = null)
    {
        if (!darfSchreiben()) return;
        var bearbeitung = new ObjektaktenBearbeitung(projekt, id, art, ergaenzungen);
        bearbeitung.PruefeBestand();
        var paketDialog = pakete is not null && dialogs is not null ? new ObjektaktenPaketDialog(pakete, dialogs) : null;
        var geo = geoShop is not null && dialogs is not null ? new GeoShopEinzelErgaenzungDialog(geoShop, dialogs, settings) : null;
        ObjektakteViewModel? vm = null;
        vm = new ObjektakteViewModel(bearbeitung, settings, geaendert, darfSchreiben, speichern,
            paketDialog is null ? null : () => paketDialog.Exportiere(projekt),
            paketDialog is null ? null : () => paketDialog.Importiere(projekt, darfSchreiben),
            feld => ListeBearbeiten(feld, () => vm),
            geo is null ? null : () => geo.ErgaenzeAsync(projekt, id, art, darfSchreiben),
            geo is null ? null : () => geo.Datei(neuWaehlen: true));
        var fenster = new ObjektakteWindow(vm);
        var owner = AktivesFenster();
        if (owner is not null) fenster.Owner = owner;
        fenster.ShowDialog();
    }

    /// <summary>Rechtsklick «Liste bearbeiten…» an einem Auswahlfeld. Oeffnet die Liste dieses Feldes -
    /// bei abhaengigen Feldern die Gruppe des gerade gewaehlten Elternwerts - und laesst die Maske
    /// danach ihre Felder neu lesen, damit die Ergaenzung sofort sichtbar ist.</summary>
    private static void ListeBearbeiten(ObjektFeldViewModel feld, Func<ObjektakteViewModel?> akte)
    {
        var bearbeitung = feld.Bearbeitung;
        var speicher = bearbeitung.ErgaenzungenSpeicher;
        if (speicher is null) return;
        var katalog = FieldCatalog.Objektfelder;
        var definition = feld.Feld;
        string katalogId;
        string? eltern = null;
        var titel = definition.Label;
        System.Collections.Generic.IEnumerable<ObjektAuswahl> basis;
        if (definition.KatalogIdJeEltern is { } voll && definition.Elternfeld is { } elternfeld)
        {
            eltern = bearbeitung.ElternCode(feld.Akte, definition);
            if (eltern is null) return; // Ohne Elternwert gibt es keine Liste, die man bearbeiten koennte.
            katalogId = voll;
            basis = katalog.Auswahl(voll)?.Eintraege.Where(e => e.Eltern == eltern) ?? [];
            titel = $"{definition.Label} · {katalog.Feld(elternfeld).Label}: {bearbeitung.Lies(feld.Akte, katalog.Feld(elternfeld))}";
        }
        else
        {
            if (definition.KatalogId is null) return;
            katalogId = definition.KatalogId;
            basis = katalog.Auswahl(katalogId)?.Eintraege ?? [];
        }
        var fenster = new ListenErgaenzungWindow(new ListenErgaenzungViewModel(
            new ListenErgaenzungBearbeitung(speicher, titel, katalogId, eltern, basis)));
        var owner = AktivesFenster();
        if (owner is not null) fenster.Owner = owner;
        fenster.ShowDialog();
        if (!fenster.ViewModel.Gespeichert) return;
        bearbeitung.ErgaenzungenNeuLaden();
        akte()?.AktualisiereFelder();
    }

    private static Window? AktivesFenster()
        => System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
}
