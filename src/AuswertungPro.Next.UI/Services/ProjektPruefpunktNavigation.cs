using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Application.UseCases.Objektakten;
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
        OeffneAkte(shell, sp, punkt, projekt, sp.ObjektaktenListenErgaenzungen);
    }

    private static void OeffneAkte(ShellViewModel shell, ServiceProvider sp, ProjektPruefpunkt punkt,
        Project projekt, IObjektaktenListenErgaenzungen ergaenzungen)
    {
        bool Bereit() => shell.IsProjectReady && shell.SaveCommand.CanExecute(null) && ReferenceEquals(shell.Project, projekt);
        // Schlusswelle (Item 5): denselben Datenaenderungsverlauf wie die Haltungs-/Schachtseite
        // (services.DatenaenderungsVerlauf) uebergeben, damit auch eine ueber die Projektpruefung
        // geoeffnete Objektakte Rueckgaengig/Wiederholen (Aufgabe 16) traegt - kein Service-Locator
        // in einer Seite, sondern derselbe DI-Wert wie ueberall sonst.
        var fabrik = ObjektaktenDialog.Fabrik(punkt.Objektart, () => shell.Project, sp.Settings,
            Bereit, () => shell.MarkProjectDirty(), () => shell.TrySaveProject(), sp.ObjektaktenPakete, sp.Dialogs,
            ergaenzungen, sp.GeoShop, sp.GeoShopSicherung, sp.DatenaenderungsVerlauf);
        var vm = fabrik(punkt.ObjektId);
        if (vm is null) return;
        var feld = BereiteFeldVor(vm, punkt);
        var vorher = Keyboard.FocusedElement;
        var fenster = new ObjektakteWindow(vm) { Owner = System.Windows.Application.Current?.MainWindow };
        if (feld is not null)
            fenster.Loaded += (_, _) => fenster.Dispatcher.BeginInvoke(() => FokussiereFeld(fenster, feld.Akte.Id, feld.Feld.Id),
                System.Windows.Threading.DispatcherPriority.Loaded);
        fenster.ShowDialog();
        if (vorher is IInputElement fokus) Keyboard.Focus(fokus);
    }

    public static async Task OeffneAsync(ShellViewModel shell, ServiceProvider sp,
        ProjektPruefpunkt punkt, Func<bool> istAktuell,
        Action<ProjektPruefungViewModel> rueckkehr, CancellationToken ct)
    {
        if (punkt.EintragId is not { } id)
        {
            if (!shell.IsProjectReady || !shell.ConfirmLeaveCurrentContext()) return;
            var projektAkte = shell.Project;
            var speicher = sp.ObjektaktenListenErgaenzungen;
            var geladen = await Task.Run(speicher.Lade, ct);
            ct.ThrowIfCancellationRequested();
            if (!istAktuell() || !shell.IsProjectReady || !ReferenceEquals(shell.Project, projektAkte)
                || !(punkt.Objektart == "haltung"
                    ? projektAkte.Data.Any(x => x.Id == punkt.ObjektId)
                    : projektAkte.SchaechteData.Any(x => x.Id == punkt.ObjektId))) return;
            OeffneAkte(shell, sp, punkt, projektAkte, new VorgeladeneListenErgaenzungen(speicher, geladen));
            return;
        }
        if (!shell.IsProjectReady || !shell.ConfirmLeaveCurrentContext()) return;
        var projekt = shell.Project;
        var h = projekt.Data.SingleOrDefault(x => x.Id == punkt.ObjektId);
        if (h is null || !istAktuell()) return;
        if (h.Protocol?.Current?.Entries.SingleOrDefault(e => e.EntryId == id && !e.IsDeleted) is null) return;
        shell.NavigateToHolding(h);
        try
        {
            if (shell.CurrentPage is DataPageViewModel dp)
                await dp.ZeigeProtokolleintragAsync(h, id, ct, istAktuell);
        }
        finally
        {
            if (shell.IsProjectReady && ReferenceEquals(shell.Project, projekt)
                && shell.CurrentPage is DataPageViewModel)
            {
                shell.NavigateTo("Uebersicht");
                if (shell.CurrentPage is ProjektUebersichtPageViewModel seite && istAktuell())
                    rueckkehr(seite.ProjektPruefung);
            }
        }
    }

    private sealed class VorgeladeneListenErgaenzungen(IObjektaktenListenErgaenzungen speicher,
        IReadOnlyList<ListenErgaenzung> geladen) : IObjektaktenListenErgaenzungen
    {
        private IReadOnlyList<ListenErgaenzung> _geladen = geladen;
        public IReadOnlyList<ListenErgaenzung> Lade() => _geladen;
        public void Speichere(IEnumerable<ListenErgaenzung> ergaenzungen)
        {
            var liste = ergaenzungen.ToArray();
            speicher.Speichere(liste);
            _geladen = liste;
        }
    }

    internal static ObjektFeldViewModel? BereiteFeldVor(ObjektakteViewModel vm, ProjektPruefpunkt punkt)
    {
        if (punkt.AkteId is { } akteId)
            vm.Auswahl = vm.Objekte.Single(x => x.Akte.Id == akteId).Akte;
        vm.Thema = "Alle Felder";
        vm.Anpassen = true;
        var feld = vm.Gruppen.SelectMany(g => g.Felder).FirstOrDefault(f =>
            punkt.FeldId is not null ? f.Feld.Id == punkt.FeldId : f.Feld.Speicherfeld == punkt.Speicherfeld);
        if (feld is not null) vm.Suche = feld.Label;
        vm.AlleAufCommand.Execute(null);
        return feld;
    }

    private static void FokussiereFeld(DependencyObject wurzel, Guid akteId, string feldId)
    {
        if (wurzel is FrameworkElement { DataContext: ObjektFeldViewModel kandidat } element
            && kandidat.Akte.Id == akteId && kandidat.Feld.Id == feldId
            && element.IsVisible && element.IsEnabled
            && element is TextBox or ComboBox)
        {
            element.BringIntoView();
            Keyboard.Focus(element);
            return;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(wurzel); i++)
            FokussiereFeld(VisualTreeHelper.GetChild(wurzel, i), akteId, feldId);
    }

    internal static void MarkiereEintrag(Window fenster, Guid id)
    {
        if (fenster.FindName("EntriesGrid") is not DataGrid grid) return;
        void Markiere()
        {
            var eintrag = grid.Items.OfType<ProtocolEntry>().SingleOrDefault(e => e.EntryId == id && !e.IsDeleted);
            if (eintrag is null) return;
            grid.SelectedItem = eintrag; grid.ScrollIntoView(eintrag); grid.UpdateLayout();
            if (grid.ItemContainerGenerator.ContainerFromItem(eintrag) is DataGridRow zeile)
                Keyboard.Focus(zeile);
            else Keyboard.Focus(grid);
        }
        if (fenster.IsLoaded) Markiere(); else fenster.Loaded += (_, _) => Markiere();
    }
}
