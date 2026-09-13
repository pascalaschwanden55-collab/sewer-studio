using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObjektaktenDropdownVollstaendigkeitTests
{
    private static (ObjektaktenBearbeitung Bearbeitung, ObjektAkte Akte) Neu(string art)
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        var a = new ObjektAkte { Art = art, Bezuege = [h.Id] };
        p.Objektakten.Add(a);
        return (b, a);
    }

    private static ObjektFeldViewModel Feld(ObjektaktenBearbeitung b, ObjektAkte a, string id)
        => new(b, a, FieldCatalog.Objektfelder.Feld(id), new(), () => { },
            fehler => throw new InvalidOperationException(fehler), () => true);

    // Unabhängiger Sollbestand aus WEBGIS-LAYOUT-KOMPLETT.md, Zeile 611.
    [Theory]
    [InlineData("Unbekannt", "0,35")]
    [InlineData("Erneuerung", "3,7,17,21,26,29,30")]
    [InlineData("Reparatur", "5,8,9,10,11,12,15,19,20,23,28,31,32,33,1000")]
    [InlineData("Renovierung", "1,2,4,6,13,14,16,18,22,24,25,27,34")]
    [InlineData("Reinigung", "")]
    [InlineData("Untersuchung", "")]
    [InlineData("Andere", "")]
    [InlineData("Begehung", "")]
    [InlineData("Deformationsmessung", "")]
    [InlineData("Dichtheitsprüfung", "")]
    [InlineData("Georadar", "")]
    [InlineData("Kanalfernsehen", "")]
    public void Sanierungsverfahren_enthalten_alle_Originalcodes_in_der_richtigen_Art(string art, string codes)
    {
        var (b, a) = Neu("sanierung");
        Feld(b, a, "sanierung.s_art").Text = art;
        var auswahl = Feld(b, a, "sanierung.s_procedure").Optionen.Where(e => e.Label.Length > 0).ToArray();
        Assert.Equal(codes.Split(',', StringSplitOptions.RemoveEmptyEntries), auswahl.Select(e => e.OriginalCode));
    }

    [Theory]
    [InlineData("deckel", "deckel.material", 5, "Guss mit Betonfüllung", "113")]
    [InlineData("sanierung", "sanierung.s_product", 15, "RS Maxliner Flex mit MaxPox 15/40", "14")]
    [InlineData("sanierung", "sanierung.s_product", 16, "RS Maxliner Flex mit MaxPox 15/40", "15")]
    [InlineData("sanierung", "sanierung.s_procedure", 12, "Schlauchverfahren", "27")]
    public void Alte_Auswahl_ohne_Code_bleibt_nach_der_Codeergaenzung_eindeutig_sichtbar(
        string art, string id, int index, string text, string code)
    {
        var (b, a) = Neu(art);
        if (art == "sanierung") Feld(b, a, "sanierung.s_art").Text = "Renovierung";
        a.Werte[id] = new ObjektFeldWert
        {
            Text = text, LokalerEintrag = index, KatalogId = FieldCatalog.Objektfelder.Feld(id).KatalogId,
            Originalcode = null, VonHand = true
        };
        var vm = Feld(b, a, id);
        Assert.Equal(text, vm.Text);
        Assert.Equal(index, vm.Auswahl?.Index);
        Assert.Equal(code, vm.Auswahl?.OriginalCode);
        // Öffnen ergänzt keine Codes in Kundendaten und macht das Projekt nicht schmutzig.
        Assert.Null(a.Werte[id].Originalcode);
    }

    [Fact]
    public void Gleicher_Text_mit_unpassender_alter_Position_wird_nicht_geraten()
    {
        var (b, a) = Neu("sanierung");
        a.Werte["sanierung.s_product"] = new()
        {
            KatalogId = "sanierung-C06", LokalerEintrag = 999,
            Text = "RS Maxliner Flex mit MaxPox 15/40", Originalcode = null
        };
        Assert.Null(Feld(b, a, "sanierung.s_product").Auswahl);
    }

    [Fact]
    public void Neuer_Reparatureintrag_wird_mit_seinem_Code_gespeichert_und_wiederangezeigt()
    {
        var (b, a) = Neu("sanierung");
        Feld(b, a, "sanierung.s_art").Text = "Reparatur";
        var vm = Feld(b, a, "sanierung.s_procedure");
        vm.Auswahl = vm.Optionen.Single(e => e.Label == "Partieller Liner");
        Assert.Equal("1000", a.Werte[vm.Feld.Id].Originalcode);
        Assert.Equal("1000", Feld(b, a, vm.Feld.Id).Auswahl?.OriginalCode);
        Feld(b, a, "sanierung.s_art").Text = "Erneuerung";
        Assert.Equal("Austausch von Bauteilen", Feld(b, a, vm.Feld.Id).Text);
        Feld(b, a, "sanierung.s_art").Text = "Reinigung";
        Assert.Empty(Feld(b, a, vm.Feld.Id).Optionen);
        Assert.Equal("Austausch von Bauteilen", Feld(b, a, vm.Feld.Id).Text);
    }

    [Fact]
    public void Haltungspunkt_ist_ueber_die_Objektauswahl_mit_allen_fuenf_Dropdowns_erreichbar()
    {
        var (b, a) = Neu("haltungspunkt");
        var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { });
        vm.Auswahl = vm.Objekte.Single(o => o.Akte.Id == a.Id).Akte;
        var dropdowns = vm.Abschnitte.SelectMany(g => g.Felder).Where(f => f.HatAuswahl).ToArray();
        Assert.Equal(5, dropdowns.Length);
        Assert.Equal(new[] { 6, 4, 6, 4, 6 }, dropdowns.Select(f => f.Optionen.Count()));
        Assert.All(dropdowns, f => Assert.Equal("", f.Optionen.First().Label));
    }

    [Theory]
    [InlineData("Untersuchung")]
    [InlineData("Begehung")]
    [InlineData("Deformationsmessung")]
    [InlineData("Georadar")]
    public void Witterung_zeigt_alle_neun_Originalwerte_fuer_die_vier_belegten_Arten(string art)
    {
        var (b, a) = Neu("unterhalt");
        Feld(b, a, "unterhalt.art").Text = art;
        var f = Feld(b, a, "unterhalt.witterung");
        Assert.Equal(new[] { "Unbekannt", "Kein Niederschlag", "Regen", "Schmelzwasser", "Bedeckt, regnerisch",
            "Nieselregen", "Schneefall", "Schön, trocken", "Frost" }, f.Optionen.Select(e => e.Label));
        f.Auswahl = f.Optionen.Single(e => e.Label == "Frost");
        Assert.Equal("8", a.Werte[f.Feld.Id].Originalcode);
        Feld(b, a, "unterhalt.art").Text = "Reinigung";
        Assert.Empty(f.Optionen);
        Assert.Equal("Frost", a.Werte[f.Feld.Id].Text);
    }

    [Fact]
    public void Jeder_Katalogeintrag_ist_in_seinem_Dropdown_erreichbar_auch_ohne_Normzuordnung()
    {
        var katalog = FieldCatalog.Objektfelder;
        foreach (var f in katalog.Felder.Where(f => f.KatalogId is not null))
        {
            var (b, a) = Neu(f.Art);
            var vm = Feld(b, a, f.Id);
            if (f.KatalogIdJeEltern is not null)
            {
                var eltern = katalog.Feld(f.Elternfeld!);
                var erreicht = new List<ObjektAuswahl>();
                foreach (var e in katalog.Auswahl(eltern.KatalogId)!.Eintraege)
                {
                    a.Werte[eltern.Id] = new() { Text = e.Label, Originalcode = e.OriginalCode };
                    erreicht.AddRange(vm.Optionen);
                }
                var erwartet = katalog.Auswahl(f.KatalogIdJeEltern)!.Eintraege;
                Assert.Equal(erwartet.OrderBy(e => e.Index), erreicht.OrderBy(e => e.Index));
            }
            else
            {
                if (f.Elternfeld is not null) a.Werte[f.Elternfeld] = new() { Text = f.BelegterElterntext ?? "" };
                Assert.Equal(katalog.Auswahl(f.KatalogId)!.Eintraege, vm.Optionen);
            }
        }
    }
}
