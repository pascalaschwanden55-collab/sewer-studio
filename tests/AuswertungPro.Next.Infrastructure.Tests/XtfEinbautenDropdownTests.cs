using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;
using static AuswertungPro.Next.Infrastructure.Tests.XtfEinbautenTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfEinbautenDropdownTests
{
    // Sollfelder aus den ILI-Klassen und den WebGIS-Masken; die Produktionszuordnung wird hier nicht verwendet.
    [Theory]
    [InlineData("FoerderAggregat", "pumpe", "bauwerksart", "Bauart")]
    [InlineData("FoerderAggregat", "pumpe", "funktion", "Funktion")]
    [InlineData("FoerderAggregat", "pumpe", "aufstellung", "AufstellungAntrieb")]
    [InlineData("FoerderAggregat", "pumpe", "aufstellung_foerderaggregat", "AufstellungFoerderaggregat")]
    [InlineData("FoerderAggregat", "pumpe", "steuerung", "Steuerung")]
    [InlineData("FoerderAggregat", "pumpe", "verstellbarkeit", "Verstellbarkeit")]
    [InlineData("FoerderAggregat", "pumpe", "signaluebermittlung", "Signaluebermittlung")]
    [InlineData("FoerderAggregat", "pumpe", "antrieb", "Antrieb")]
    [InlineData("Absperr_Drosselorgan", "absperr_drossel", "bauwerksart", "Art")]
    [InlineData("Absperr_Drosselorgan", "absperr_drossel", "steuerung", "Steuerung")]
    [InlineData("Absperr_Drosselorgan", "absperr_drossel", "verstellbarkeit", "Verstellbarkeit")]
    [InlineData("Absperr_Drosselorgan", "absperr_drossel", "signaluebermittlung", "Signaluebermittlung")]
    [InlineData("Absperr_Drosselorgan", "absperr_drossel", "antrieb", "Antrieb")]
    [InlineData("Leapingwehr", "ueberlauf", "oeffnungsform", "Oeffnungsform")]
    [InlineData("Streichwehr", "ueberlauf", "ueberfallkante", "Ueberfallkante")]
    [InlineData("Leapingwehr", "ueberlauf", "funktion", "Funktion")]
    [InlineData("Leapingwehr", "ueberlauf", "steuerung", "Steuerung")]
    [InlineData("Leapingwehr", "ueberlauf", "verstellbarkeit", "Verstellbarkeit")]
    [InlineData("Leapingwehr", "ueberlauf", "signaluebermittlung", "Signaluebermittlung")]
    [InlineData("Leapingwehr", "ueberlauf", "antrieb", "Antrieb")]
    [InlineData("Streichwehr", "ueberlauf", "funktion", "Funktion")]
    [InlineData("Streichwehr", "ueberlauf", "steuerung", "Steuerung")]
    [InlineData("Streichwehr", "ueberlauf", "verstellbarkeit", "Verstellbarkeit")]
    [InlineData("Streichwehr", "ueberlauf", "signaluebermittlung", "Signaluebermittlung")]
    [InlineData("Streichwehr", "ueberlauf", "antrieb", "Antrieb")]
    [InlineData("Einstiegshilfe", "bauwerksteil", "subart", "Art")]
    [InlineData("Einstiegshilfe", "bauwerksteil", "instandstellung", "Instandstellung")]
    [InlineData("Trockenwetterfallrohr", "bauwerksteil", "instandstellung", "Instandstellung")]
    public void Jede_angebotene_Auswahl_wird_exportiert_und_wieder_als_gleiche_Auswahl_importiert(
        string klasse, string art, string feld, string attribut)
    {
        var p = Probe(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == art && a.Quellen.Any(q => q.Klasse == klasse));
        var f = FieldCatalog.Objektfelder.Feld(art + "." + feld);
        var b = new ObjektaktenBearbeitung(p, p.SchaechteData[0].Id, "schacht");
        var auswahl = b.ErlaubteEintraege(a, f).ToArray();
        Assert.NotEmpty(auswahl);
        foreach (var e in auswahl)
        {
            b.Schreibe(a, f, b.Lies(a, f), e.Label, e);
            string norm = "";
            WithExport(p, doc => norm = Wert(doc, klasse, attribut));
            Assert.False(string.IsNullOrEmpty(norm), $"{f.Id}: {e.Label}");
            Assert.DoesNotMatch(@"^\d+$", norm);
            var neu = Probe();
            neu.Objektakten[1].Quellen.Single(q => q.Klasse == klasse).Werte[attribut] = norm;
            Importiere(neu);
            var wieder = neu.Objektakten.Single(a => a.Art == art && a.Quellen.Any(q => q.Klasse == klasse));
            Assert.Equal(e.Label, wieder.Werte[f.Id].Text);
        }
    }

    [Fact]
    public void Einheit_des_WebGIS_Arbeitspunkts_wird_nicht_als_Normeinheit_geraten()
    {
        var p = Probe(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "pumpe");
        a.Werte["pumpe.arbeitspunkt_m³"] = new() { Text = "12", VonHand = true };
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(r.Ok, r.Fehler);
        Assert.Contains("Arbeitspunkt [m³] = „12“ fehlt in der XTF", r.Bericht);
    }
}
