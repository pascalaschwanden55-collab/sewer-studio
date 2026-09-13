using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObjektaktenEinbautenTests
{
    private const string Knoten = "chTEST0000000010", Bauwerk = "chTEST0000000011";

    [Theory]
    [InlineData("pumpe", "FoerderAggregat")]
    [InlineData("absperr_drossel", "Absperr_Drosselorgan")]
    [InlineData("ueberlauf", "Leapingwehr")]
    public void Importierter_Einbau_hat_seine_Dropdowns_und_liest_geerbte_Werte_am_richtigen_Schacht(string art, string klasse)
    {
        var p = new Project(); var s = new SchachtRecord { Geonis = new() { Knoten = Knoten, Bauwerk = Bauwerk } };
        s.SetFieldValue("Schachtnummer", "Schacht B", FieldSource.Kataster, false);
        s.SetFieldValue("Bemerkungen", "Bemerkung am Bauwerk", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        var quelle = new GeoShopBauteil("Schacht B", KatasterKennung.FuerSchacht("Schacht B", null, Knoten, Bauwerk), new Dictionary<string, string>(), Quellen:
        [
            Q("Abwasserknoten", Knoten, new() { ["Bezeichnung"] = "Schacht B", ["Sohlenkote"] = "450.000" }, new() { ["AbwasserbauwerkRef"] = Bauwerk }),
            Q("Normschacht", Bauwerk, new() { ["Bezeichnung"] = "Bauwerk B", ["Bemerkung"] = "Original-Bauwerk" }, new()),
            Q(klasse, "chTEST0000000030", new() { ["Bezeichnung"] = "Eigener Einbau-Name", ["Bemerkung"] = "Eigene Einbau-Bemerkung" }, new() { ["AbwasserknotenRef"] = Knoten })
        ]);
        GeoShopObjektaktenImport.Uebernehme(p, s.Id, "schacht", quelle, false);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { });
        var einbau = vm.Objekte.Single(o => o.Akte.Art == art).Akte;
        vm.Auswahl = einbau;
        var felder = vm.KopfFelder.Concat(vm.Abschnitte.SelectMany(g => g.Felder)).ToArray();
        Assert.Equal(FieldCatalog.Objektfelder.Felder.Count(f => f.Art == art && f.KatalogId is not null), felder.Count(f => f.HatAuswahl));
        Assert.Equal("Schacht B", felder.Single(f => f.Feld.Id == art + ".bezeichnung").Text);
        Assert.Equal("Bemerkung am Bauwerk", felder.Single(f => f.Feld.Id == art + ".bemerkung").Text);
        Assert.False(felder.Single(f => f.Feld.Id == art + ".bemerkung").Bearbeitbar);
        Assert.Equal("Eigene Einbau-Bemerkung", einbau.Quellen.Single().Werte["Bemerkung"]);
        s.SetFieldValue("Bemerkungen", "", FieldSource.Manual, true);
        Assert.Equal("", b.Lies(einbau, FieldCatalog.Objektfelder.Feld(art + ".bemerkung")));
        Assert.Throws<InvalidOperationException>(() => b.Schreibe(einbau, FieldCatalog.Objektfelder.Feld(art + ".bezeichnung"), "Schacht B", "Falsch"));
    }

    [Fact]
    public void Bauwerksteil_steht_nach_Import_nur_einmal_in_der_Liste()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var quelle = new GeoShopBauteil("B", KatasterKennung.FuerSchacht("B", null, Knoten, Bauwerk), new Dictionary<string, string>(), Quellen:
            [Q("Einstiegshilfe", "chTEST0000000014", new() { ["Bezeichnung"] = "Leiter B", ["Art"] = "Leiter" }, new() { ["AbwasserbauwerkRef"] = Bauwerk })]);
        GeoShopObjektaktenImport.Uebernehme(p, s.Id, "schacht", quelle, false);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var liste = FieldCatalog.Objektfelder.Unterlisten.Single(l => l.Art == "schacht" && l.Label == "Bauwerksteile");
        Assert.Single(ObjektaktenListen.Akten(b, b.Wurzel, liste));
        Assert.Empty(ObjektaktenListen.Zeilen(b, b.Wurzel, liste));
    }

    private static ObjektQuellbeleg Q(string klasse, string id, Dictionary<string, string> werte, Dictionary<string, string> refs)
        => new() { System = "GeoShop-XTF", Modell = "DSS_2020_1_LV95", Klasse = klasse, Kennung = id, Werte = werte, Referenzen = refs };
}
