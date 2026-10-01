using System.Collections.Concurrent;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Fenster «Verteilen»: Schritte ein-/ausblenden, Vorschau neu rechnen, Auftrag zurückgeben.</summary>
public sealed class VerteilenViewModelTests
{
    [Fact]
    public void Artwechsel_blendet_filme_und_ablage_passend_ein_und_aus()
    {
        using var vm = Neu(VerteilArt.Haltungen, out _, out _);
        Assert.True(vm.ZeigeFilme);
        Assert.True(vm.ZeigeAblage);
        Assert.True(vm.ZeigeTxtUmschalter);

        vm.IstSchaechte = true;
        Assert.False(vm.ZeigeFilme);
        Assert.True(vm.ZeigeAblage);
        Assert.False(vm.ZeigeTxtUmschalter);

        vm.IstDichtheit = true;
        Assert.False(vm.ZeigeFilme);
        Assert.False(vm.ZeigeAblage);
        Assert.False(vm.ZeigeSanierungHinweis);
    }

    [Fact]
    public void Sanierung_zeigt_den_hinweis_zum_unterordner()
    {
        using var vm = Neu(VerteilArt.Schaechte, out _, out _);
        Assert.False(vm.ZeigeSanierungHinweis);

        vm.IstSanierung = true;

        Assert.True(vm.ZeigeSanierungHinweis);
        Assert.Contains("_Saniert", vm.SanierungHinweis, StringComparison.Ordinal);
    }

    [Fact]
    public void Art_bestimmt_den_angezeigten_hauptordner()
    {
        using var vm = Neu(VerteilArt.Haltungen, out _, out _);
        Assert.Equal(@"C:\Ziel\Haltungen", vm.Ziel.Ordner);

        vm.IstSchaechte = true;

        Assert.Equal(@"C:\Ziel\Schaechte", vm.Ziel.Ordner);
        Assert.Contains("Schaechte", vm.ZielAnzeige, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quellwahl_rechnet_die_vorschau_und_jede_aenderung_rechnet_neu()
    {
        using var vm = Neu(VerteilArt.Haltungen, out var vorschau, out var dialoge);
        dialoge.Ordner = @"C:\Quelle";

        vm.QuelleOrdnerWaehlenCommand.Execute(null);
        await vm.LaufendeVorschau;

        var erste = Assert.Single(vorschau.Anfragen);
        Assert.Equal(VerteilQuelle.PdfOrdner(@"C:\Quelle").Ordner, erste.Quelle.Ordner);
        Assert.Equal(@"C:\Filme", erste.FilmOrdner);
        Assert.Equal(@"C:\Ziel\Haltungen", erste.Zielordner);
        Assert.Equal("2 Dateien · 1 wird abgelegt · 1 braucht Aufmerksamkeit", vm.Kopfzeile);
        Assert.Equal(2, vm.Zeilen.Count);

        vm.IstSanierung = true;
        await vm.LaufendeVorschau;
        Assert.Equal(DistributionVariant.Sanierung, vorschau.Anfragen.Last().Ablage);

        vm.IstSchaechte = true;
        await vm.LaufendeVorschau;
        var letzte = vorschau.Anfragen.Last();
        Assert.Equal(VerteilArt.Schaechte, letzte.Art);
        Assert.Null(letzte.FilmOrdner);
        Assert.Equal(@"C:\Quelle", letzte.Quelle.Ordner);
    }

    [Fact]
    public async Task Jetzt_verteilen_gibt_genau_die_gewaehlte_quelle_zurueck()
    {
        using var vm = Neu(VerteilArt.Haltungen, out _, out var dialoge);
        var geschlossen = 0;
        vm.SchliessenAngefordert += (_, _) => geschlossen++;
        dialoge.Dateien = [@"C:\Quelle\a.pdf", @"C:\Quelle\b.pdf"];

        vm.EinzelneDateienWaehlenCommand.Execute(null);
        await vm.LaufendeVorschau;
        Assert.True(vm.VerteilenCommand.CanExecute(null));
        Assert.Equal("Jetzt verteilen (1 Datei)", vm.VerteilenText);

        vm.VerteilenCommand.Execute(null);

        Assert.Equal(1, geschlossen);
        var auftrag = Assert.IsType<VerteilAuftrag>(vm.Rueckgabe.Auftrag);
        Assert.Equal(VerteilArt.Haltungen, auftrag.Art);
        Assert.Equal(VerteilQuellenArt.PdfDateien, auftrag.Quelle.Art);
        Assert.Equal(dialoge.Dateien, auftrag.Quelle.Dateien);
        Assert.Equal(@"C:\Filme", auftrag.FilmOrdner);
    }

    [Fact]
    public async Task Ohne_filmordner_laesst_sich_eine_haltung_nicht_verteilen()
    {
        using var vm = Neu(VerteilArt.Haltungen, out _, out var dialoge, filmOrdner: null);
        dialoge.Ordner = @"C:\Quelle";

        vm.QuelleOrdnerWaehlenCommand.Execute(null);
        await vm.LaufendeVorschau;

        Assert.False(vm.VerteilenCommand.CanExecute(null));

        dialoge.Ordner = @"C:\Filme";
        vm.FilmOrdnerWaehlenCommand.Execute(null);
        await vm.LaufendeVorschau;

        Assert.True(vm.VerteilenCommand.CanExecute(null));
    }

    [Fact]
    public async Task Txt_umschalten_verwirft_die_pdf_auswahl_und_waehlt_txt()
    {
        using var vm = Neu(VerteilArt.Haltungen, out var vorschau, out var dialoge);
        dialoge.Ordner = @"C:\Quelle";
        vm.QuelleOrdnerWaehlenCommand.Execute(null);
        await vm.LaufendeVorschau;

        vm.TxtUmschaltenCommand.Execute(null);

        Assert.Null(vm.Quelle);
        Assert.False(vm.VerteilenCommand.CanExecute(null));
        vm.QuelleOrdnerWaehlenCommand.Execute(null);
        await vm.LaufendeVorschau;
        Assert.Equal(VerteilQuellenArt.TxtOrdner, vorschau.Anfragen.Last().Quelle.Art);
    }

    [Fact]
    public void Dichtheit_verteilt_immer_normal()
    {
        using var vm = Neu(VerteilArt.Haltungen, out _, out var dialoge);
        vm.IstSanierung = true;
        vm.IstDichtheit = true;
        dialoge.Ordner = @"C:\Quelle";
        vm.QuelleOrdnerWaehlenCommand.Execute(null);

        Assert.Equal(DistributionVariant.Normal, vm.Auftrag!.Ablage);
        Assert.Null(vm.Auftrag.FilmOrdner);
    }

    [Fact]
    public void Abbrechen_und_einstellungen_geben_keinen_auftrag()
    {
        using var vm = Neu(VerteilArt.Haltungen, out _, out _);
        vm.AbbrechenCommand.Execute(null);
        Assert.Null(vm.Rueckgabe.Auftrag);
        Assert.False(vm.Rueckgabe.EinstellungenOeffnen);

        vm.EinstellungenOeffnenCommand.Execute(null);
        Assert.Null(vm.Rueckgabe.Auftrag);
        Assert.True(vm.Rueckgabe.EinstellungenOeffnen);
    }

    private static VerteilenViewModel Neu(
        VerteilArt art,
        out VorschauFake vorschau,
        out DialogFake dialoge,
        string? filmOrdner = @"C:\Filme")
    {
        vorschau = new VorschauFake();
        dialoge = new DialogFake();
        var vorgabe = new VerteilenVorgabe(
            art,
            DistributionVariant.Normal,
            filmOrdner,
            Projekt: null,
            a => new VerteilZiel(
                a == VerteilArt.Schaechte ? @"C:\Ziel\Schaechte" : @"C:\Ziel\Haltungen",
                a == VerteilArt.Schaechte ? "Projektordner \\ Schaechte" : "Projektordner \\ Haltungen",
                Baum: null));
        return new VerteilenViewModel(vorgabe, vorschau, dialoge);
    }

    private sealed class VorschauFake : IVerteilVorschau
    {
        private readonly ConcurrentQueue<VerteilVorschauAnfrage> _anfragen = new();

        public IReadOnlyList<VerteilVorschauAnfrage> Anfragen => _anfragen.ToArray();

        public VerteilVorschauErgebnis Plane(
            VerteilVorschauAnfrage anfrage,
            IProgress<VerteilVorschauFortschritt>? fortschritt,
            CancellationToken abbruch)
        {
            _anfragen.Enqueue(anfrage);
            return new VerteilVorschauErgebnis(2,
            [
                new VerteilVorschauZeile("a.pdf", "1-2", "1-2", "film.mp4", VerteilVorschauStatus.FilmGefunden, ""),
                new VerteilVorschauZeile("b.pdf", null, null, null, VerteilVorschauStatus.NichtZugeordnet, "nicht erkannt"),
            ], []);
        }
    }

    private sealed class DialogFake : IDialogService
    {
        public string? Ordner { get; set; }
        public string[] Dateien { get; set; } = [];

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => Dateien;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => Ordner;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestätigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Cancel;
    }
}
