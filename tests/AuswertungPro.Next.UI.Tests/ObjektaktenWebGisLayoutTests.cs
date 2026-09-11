using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObjektaktenWebGisLayoutTests
{
    [Theory]
    [InlineData("haltung")]
    [InlineData("schacht")]
    [InlineData("deckel")]
    [InlineData("sanierung")]
    public void Jeder_Wert_bleibt_genau_einmal_in_der_WebGis_Ansicht(string art)
    {
        var p = new Project(); var h = new HaltungRecord(); var s = new SchachtRecord();
        p.Data.Add(h); p.SchaechteData.Add(s);
        var b = art == "haltung" ? new ObjektaktenBearbeitung(p, h.Id, "haltung") : new(p, s.Id, "schacht");
        var akte = art is "haltung" or "schacht" ? b.Wurzel : b.Neu(art);
        var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { }) { Auswahl = akte };
        var erwartet = vm.Gruppen.SelectMany(g => g.Felder).Select(f => f.Feld.Id).Order().ToArray();
        var sichtbar = vm.KopfFelder.Concat(vm.Abschnitte.SelectMany(a => a.Felder)).Select(f => f.Feld.Id).Order().ToArray();
        Assert.Equal(erwartet, sichtbar);
        Assert.All(vm.Abschnitte, a => Assert.False(a.Offen));
        vm.AlleAufCommand.Execute(null); Assert.All(vm.Abschnitte, a => Assert.True(a.Offen));
        vm.AlleZuCommand.Execute(null); Assert.All(vm.Abschnitte, a => Assert.False(a.Offen));
    }

    [Fact]
    public void Haltung_hat_die_WebGis_Reihenfolge_und_keine_erfundenen_Themen()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var vm = new ObjektakteViewModel(new(p, h.Id, "haltung"), new(), () => { }, () => true, () => { });
        Assert.Equal(["Daten I", "Daten II", "Bauwerksteile", "Haltungspunkte", "Administrativ", "Unterhalt", "Hydraulik", "Metadaten"],
            vm.Abschnitte.Where(a => a.Titel != "SewerStudio").Select(a => a.Titel));
        Assert.Equal(["haltung.name", "haltung.objectid", "haltung.altname", "haltung.historic", "haltung.fromnode", "haltung.tonode"],
            vm.KopfFelder.Select(f => f.Feld.Id));
        Assert.Equal("Administrativ", ObjektaktenWebGisLayout.Bereich(FieldCatalog.Objektfelder.Feld("haltung.owner")));
        var unterhalt = vm.Abschnitte.Single(a => a.Titel == "Unterhalt");
        unterhalt.Offen = true;
        unterhalt.Felder.Single(f => f.Feld.Id == "haltung.flushinterval").Text = "2";
        Assert.Same(unterhalt, vm.Abschnitte.Single(a => a.Titel == "Unterhalt"));
        Assert.True(unterhalt.Offen);
        vm.Suche = "Spülintervall";
        Assert.All(vm.Abschnitte, a => Assert.True(a.Offen));
        Assert.Empty(p.Objektakten.Single().Quellen);
    }
}
