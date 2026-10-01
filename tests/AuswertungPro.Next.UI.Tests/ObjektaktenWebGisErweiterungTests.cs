using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObjektaktenWebGisErweiterungTests
{
    [Fact]
    public void Einzelnes_Aufklappen_schliesst_den_vorherigen_Bereich()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var vm = new ObjektakteViewModel(new(p, h.Id, "haltung"), new(), () => { }, () => true, () => { });
        vm.Abschnitte[0].Offen = true;
        vm.Abschnitte[1].Offen = true;
        Assert.False(vm.Abschnitte[0].Offen);
        Assert.Single(vm.Abschnitte.Where(a => a.Offen));
        vm.AlleAufCommand.Execute(null); Assert.All(vm.Abschnitte, a => Assert.True(a.Offen));
    }

    [Fact]
    public void Einbauten_stehen_im_belegten_eigenen_Bereich()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var vm = new ObjektakteViewModel(new(p, s.Id, "schacht"), new(), () => { }, () => true, () => { });
        var a = Assert.Single(vm.Abschnitte.Where(a => a.Titel == "Einbauten"));
        Assert.Equal(4, a.Listen.Count);
        Assert.Contains(a.Listen, l => l.Titel == "Pumpen");
        Assert.DoesNotContain(vm.Abschnitte.Single(a => a.Titel == "Bauwerksteile").Listen, l => l.Titel == "Pumpen");
    }

    [Fact]
    public void Listen_blaettern_in_Sechsergruppen_ohne_verlorene_oder_doppelte_Zeilen()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        for (var i = 0; i < 13; i++) b.Neu("unterhalt");
        var def = FieldCatalog.Objektfelder.Unterlisten.Single(l => l.Art == "haltung" && l.ZeigtAufObjektart == "unterhalt");
        ObjektAkte? geoeffnet = null;
        var liste = new ObjektListenAnzeige(b, b.Wurzel, def, a => geoeffnet = a, () => true, _ => { });
        var alle = new List<Guid>();
        Assert.False(liste.VorigeSeiteCommand.CanExecute(null));
        foreach (var erwartet in new[] { 6, 6, 1 })
        {
            Assert.Equal(erwartet, liste.SichtbareZeilen.Count);
            alle.AddRange(liste.SichtbareZeilen.Select(z => z.Akte!.Id));
            if (liste.NaechsteSeiteCommand.CanExecute(null)) liste.NaechsteSeiteCommand.Execute(null);
        }
        Assert.Equal(13, alle.Distinct().Count());
        Assert.False(liste.NaechsteSeiteCommand.CanExecute(null));
        liste.OeffnenCommand.Execute(liste.SichtbareZeilen[0]);
        Assert.Equal(alle[^1], geoeffnet!.Id);
        liste.Seite = 99; Assert.Equal(2, liste.Seite);
        liste.Seite = -1; Assert.Equal(0, liste.Seite);
    }

    [Fact]
    public void Saemtliche_Katalogfelder_bleiben_in_genau_einem_Bereich()
    {
        foreach (var art in FieldCatalog.Objektfelder.Felder.Select(f => f.Art).Distinct())
        {
            var p = new Project(); var h = new HaltungRecord(); var s = new SchachtRecord(); p.Data.Add(h); p.SchaechteData.Add(s);
            var wurzelart = art == "haltungspunkt" ? "haltung" : art is "haltung" or "schacht" ? art
                : FieldCatalog.Objektfelder.Unterlisten.First(l => l.ZeigtAufObjektart == art).Art;
            var b = new ObjektaktenBearbeitung(p, wurzelart == "haltung" ? h.Id : s.Id, wurzelart);
            var akte = b.Wurzel;
            if (art is not ("haltung" or "schacht"))
            {
                // Auch die nur am Bezugsobjekt pflegbaren Einbauten können als importierte Akten vorliegen.
                akte = new ObjektAkte { Art = art, Bezuege = [b.WurzelId] };
                p.Objektakten.Add(akte);
            }
            var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { }) { Auswahl = akte };
            var ids = vm.KopfFelder.Concat(vm.Abschnitte.SelectMany(a => a.Felder)).Select(f => f.Feld.Id).ToArray();
            Assert.Equal(ids.Length, ids.Distinct().Count());
            foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == art)) Assert.Contains(f.Id, ids);
        }
    }
}
