using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

internal static class ProjektPruefpunktNavigation
{
    public static void Oeffne(ShellViewModel shell, ServiceProvider sp, ProjektPruefpunkt punkt)
    {
        if (!shell.IsProjectReady || !shell.ConfirmLeaveCurrentContext()) return;
        var projekt = shell.Project;
        var h = projekt.Data.SingleOrDefault(h => h.Id == punkt.ObjektId);
        var s = projekt.SchaechteData.SingleOrDefault(s => s.Id == punkt.ObjektId);
        if (punkt.Objektart == "haltung" ? h is null : s is null) return;
        if (punkt.EintragId is { } id && h is not null)
        {
            shell.NavigateToHolding(h);
            if (shell.CurrentPage is DataPageViewModel dp) dp.ZeigeProtokolleintrag(h, id);
            return;
        }
        bool Bereit() => shell.IsProjectReady && shell.SaveCommand.CanExecute(null) && ReferenceEquals(shell.Project, projekt);
        var fabrik = ObjektaktenDialog.Fabrik(punkt.Objektart, () => shell.Project, sp.Settings,
            Bereit, () => shell.MarkProjectDirty(), () => shell.TrySaveProject(), sp.ObjektaktenPakete, sp.Dialogs,
            sp.ObjektaktenListenErgaenzungen, sp.GeoShop, sp.GeoShopSicherung);
        var vm = fabrik(punkt.ObjektId);
        if (vm is null) return;
        BereiteFeldVor(vm, punkt);
        new ObjektakteWindow(vm) { Owner = System.Windows.Application.Current?.MainWindow }.ShowDialog();
    }

    internal static void BereiteFeldVor(ObjektakteViewModel vm, ProjektPruefpunkt punkt)
    {
        if (punkt.AkteId is { } akteId)
            vm.Auswahl = vm.Objekte.Single(x => x.Akte.Id == akteId).Akte;
        vm.Thema = "Alle Felder";
        vm.Anpassen = true;
        var feld = vm.Gruppen.SelectMany(g => g.Felder).FirstOrDefault(f =>
            punkt.FeldId is not null ? f.Feld.Id == punkt.FeldId : f.Feld.Speicherfeld == punkt.Speicherfeld);
        if (feld is not null) vm.Suche = feld.Label;
        vm.AlleAufCommand.Execute(null);
    }

    internal static void MarkiereEintrag(Window fenster, Guid id)
    {
        if (fenster.FindName("EntriesGrid") is not DataGrid grid) return;
        void Markiere()
        {
            var eintrag = grid.Items.OfType<ProtocolEntry>().SingleOrDefault(e => e.EntryId == id && !e.IsDeleted);
            if (eintrag is null) return;
            grid.SelectedItem = eintrag; grid.ScrollIntoView(eintrag); grid.Focus();
        }
        if (fenster.IsLoaded) Markiere(); else fenster.Loaded += (_, _) => Markiere();
    }
}
