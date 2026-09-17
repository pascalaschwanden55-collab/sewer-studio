using System.IO;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektPruefungViewModelTests
{
    private sealed class Dienst(Func<Project, CancellationToken, ProjektPruefergebnis> pruefe) : IProjektPruefung
    {
        public ProjektPruefergebnis Pruefe(Project p, string? pfad, CancellationToken ct) => pruefe(p, ct);
    }

    private static ProjektPruefungViewModel Vm(IProjektPruefung dienst, Func<(Project, string?)> aktuell,
        Action<ProjektPruefpunkt>? oeffnen = null)
        => new(dienst, aktuell, new JsonProjectRepository().DeepCopy,
            new JsonProjectContentSignature().Compute, () => true, oeffnen ?? (_ => { }));

    [Fact]
    public async Task Echte_Pruefung_liest_Kopie_und_oeffnet_genau_den_gefundenen_Befund()
    {
        var p = new Project(); var h = new HaltungRecord { Protocol = new() { Current = new() } };
        p.Data.Add(h); h.Protocol.Current.Entries.Add(new() { Code = "BAB", Ai = new() });
        ProjektPruefpunkt? ziel = null;
        var dienst = new Dienst((kopie, ct) =>
        {
            Assert.NotSame(p, kopie); Assert.NotSame(h, kopie.Data[0]);
            return ProjektPruefregeln.Pruefe(kopie, _ => null, ct);
        });
        using var vm = Vm(dienst, () => (p, null), x => ziel = x);
        await vm.PruefenCommand.ExecuteAsync(null);
        Assert.True(vm.IstAktuell);
        var punkt = Assert.Single(vm.Punkte);
        vm.OeffnenCommand.Execute(punkt);
        Assert.Equal(h.Protocol.Current.Entries[0].EntryId, ziel!.EintragId);
        // Protokolleintraege melden keine PropertyChanged-Ereignisse: die Signatur muss schuetzen.
        h.Protocol.Current.Entries[0].Ai!.Accepted = true; ziel = null;
        vm.OeffnenCommand.Execute(punkt);
        Assert.Null(ziel); Assert.False(vm.IstAktuell); Assert.Empty(vm.Punkte);
    }

    [Theory]
    [InlineData("Projektwechsel")] [InlineData("Daten")] [InlineData("Pfad")]
    [InlineData("Abbruch")] [InlineData("Dispose")]
    public async Task Spaetes_Ergebnis_wird_nach_Wechsel_Aenderung_oder_Abbruch_verworfen(string fall)
    {
        var p = new Project(); string? pfad = "C:/eins/projekt.json";
        var gestartet = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var freigabe = new ManualResetEventSlim();
        using var vm = Vm(new Dienst((_, _) =>
        {
            gestartet.TrySetResult();
            if (!freigabe.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new([], 1, 0);
        }), () => (p, pfad));
        var lauf = vm.PruefenCommand.ExecuteAsync(null);
        try
        {
            await gestartet.Task.WaitAsync(TimeSpan.FromSeconds(5));
            switch (fall)
            {
                case "Projektwechsel": p = new Project(); break;
                case "Daten": p.Data.Add(new HaltungRecord()); break;
                case "Pfad": pfad = "C:/zwei/projekt.json"; break;
                case "Abbruch": vm.AbbrechenCommand.Execute(null); break;
                case "Dispose": vm.Dispose(); break;
            }
        }
        finally { freigabe.Set(); }
        await lauf.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(vm.IstAktuell); Assert.Empty(vm.Punkte);
        Assert.DoesNotContain("0 Hinweise", vm.Meldung);
    }

    [Fact]
    public async Task Nicht_mehr_lesbarer_Projektstand_wird_vor_dem_Sprung_abgefangen()
    {
        var p = new Project(); var h = new HaltungRecord { Protocol = new() { Current = new() } };
        p.Data.Add(h); h.Protocol.Current.Entries.Add(new() { Ai = new() });
        var geoeffnet = false;
        using var vm = Vm(new Dienst((kopie, ct) => ProjektPruefregeln.Pruefe(kopie, _ => null, ct)),
            () => (p, null), _ => geoeffnet = true);
        await vm.PruefenCommand.ExecuteAsync(null);
        var punkt = Assert.Single(vm.Punkte);
        h.Protocol.Current.Entries[0].MeterStart = double.NaN;
        vm.OeffnenCommand.Execute(punkt);
        Assert.False(geoeffnet); Assert.False(vm.IstAktuell); Assert.Empty(vm.Punkte);
        Assert.Contains("Stelle konnte nicht", vm.Meldung);
    }

    [Fact]
    public async Task Dienstfehler_und_leeres_Projekt_werden_nicht_als_Freigabe_gemeldet()
    {
        var p = new Project();
        using var fehler = Vm(new Dienst((_, _) => throw new IOException("Testfehler")), () => (p, null));
        await fehler.PruefenCommand.ExecuteAsync(null);
        Assert.False(fehler.IstAktuell); Assert.Contains("Testfehler", fehler.Meldung);
        using var leer = Vm(new Dienst((_, _) => new([], 0, 0)), () => (p, null));
        await leer.PruefenCommand.ExecuteAsync(null);
        Assert.Contains("keine Haltungen", leer.Meldung);
    }

    [Fact]
    public void Datensatzbeobachter_erfasst_Erledigt_und_neue_Zeilen_und_meldet_sich_ab()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h); var anzahl = 0;
        using var beobachter = new ProjektDatensatzBeobachter(p, () => anzahl++);
        h.BearbeitungErledigt = true; Assert.True(anzahl > 0);
        var s = new SchachtRecord(); p.SchaechteData.Add(s); var stand = anzahl;
        s.SetFieldValue("Tiefe", "-2", FieldSource.Manual, true); Assert.True(anzahl > stand);
        p.Data.Remove(h); stand = anzahl;
        h.BearbeitungErledigt = false; Assert.Equal(stand, anzahl);
        beobachter.Dispose(); stand = anzahl;
        s.SetFieldValue("Tiefe", "3", FieldSource.Manual, true); p.Data.Add(h);
        Assert.Equal(stand, anzahl);
    }

    [Fact]
    public void Feldsprung_waehlt_die_richtige_Unterakte_und_macht_das_Feld_sichtbar()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var b = new AuswertungPro.Next.Application.UseCases.Objektakten.ObjektaktenBearbeitung(p, h.Id, "haltung");
        var bauteil = b.Neu("bauwerksteil");
        var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { });
        ProjektPruefpunktNavigation.BereiteFeldVor(vm, new(ProjektPruefbereich.Eingabefelder,
            "haltung", h.Id, "1-2", "Pflichtfeld fehlt", AkteId: bauteil.Id, FeldId: "bauwerksteil.bezeichnung"));
        Assert.Same(bauteil, vm.Auswahl);
        Assert.Equal(FieldCatalog.Objektfelder.Feld("bauwerksteil.bezeichnung").Label, vm.Suche);
        Assert.Contains(vm.Gruppen.SelectMany(g => g.Felder), f => f.Feld.Id == "bauwerksteil.bezeichnung");
    }
}
