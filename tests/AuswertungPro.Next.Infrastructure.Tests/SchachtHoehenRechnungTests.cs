using System.Text.Json;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtHoehenRechnungTests
{
    [Theory]
    [InlineData("520.600", "517.710", "", "schacht.tiefe", "2.890")]
    [InlineData("", "517.710", "2.890", "schacht.deckelhoehe", "520.600")]
    [InlineData("520.600", "", "2.890", "schacht.sohlenhoehe", "517.710")]
    public void Zwei_Werte_liefern_den_dritten_ohne_Originale_zu_veraendern(string deckel, string sohle, string tiefe, string feld, string erwartet)
    {
        var b = Beispiel(deckel, sohle, tiefe);
        var vorher = JsonSerializer.Serialize(b.Projekt);
        var r = SchachtHoehenRechnung.Fuer(b);
        Assert.Equal(erwartet, r.Lies(feld));
        Assert.Equal(feld, r.BerechnetesFeld);
        Assert.Equal(erwartet, b.Lies(b.Wurzel, FieldCatalog.Objektfelder.Feld(feld)));
        Assert.Contains("Berechnet", r.Hinweis(feld));
        Assert.Equal(vorher, JsonSerializer.Serialize(b.Projekt));
        var neu = JsonSerializer.Deserialize<Project>(vorher)!;
        Assert.Equal(erwartet, SchachtHoehenRechnung.Fuer(new(neu, b.WurzelId, "schacht")).Lies(feld));
    }

    [Theory]
    [InlineData("520.600", "517.710", "2.800")]
    [InlineData("520.600", "517.710", "-2.890")]
    [InlineData("520.600", "521.000", "")]
    public void Widerspruch_oder_negative_Tiefe_wird_gemeldet_und_nicht_weggerechnet(string d, string s, string t)
    {
        var r = SchachtHoehenRechnung.Fuer(Beispiel(d, s, t));
        Assert.Null(r.BerechnetesFeld); Assert.NotEmpty(r.Warnung);
        Assert.Equal(d, r.Lies(SchachtHoehenRechnung.Deckelfeld));
        Assert.Equal(s, r.Lies(SchachtHoehenRechnung.Sohlenfeld));
        Assert.Equal(t, r.Lies(SchachtHoehenRechnung.Tiefenfeld));
    }

    [Fact]
    public void Bewusst_leere_Sohle_und_Deckelhoehe_bleiben_geschuetzt()
    {
        var b = Beispiel("520.600", "", "2.890");
        b.Wurzel.Werte[SchachtHoehenRechnung.Sohlenfeld].VonHand = true;
        Assert.Equal("", SchachtHoehenRechnung.Fuer(b).Lies(SchachtHoehenRechnung.Sohlenfeld));
        b = Beispiel("", "517.710", "2.890");
        b.Projekt.Objektakten.Single(a => a.Art == "deckel").Werte["deckel.hoehe"].VonHand = true;
        Assert.Equal("", SchachtHoehenRechnung.Fuer(b).Lies(SchachtHoehenRechnung.Deckelfeld));
    }

    [Fact]
    public void Mehrere_Deckel_ohne_Auswahl_erzeugen_keine_erfundene_Deckelhoehe()
    {
        var b = Beispiel("", "517.710", "2.890");
        b.Projekt.Objektakten.Add(new() { Art = "deckel", Bezuege = [b.WurzelId] });
        var r = SchachtHoehenRechnung.Fuer(b);
        Assert.Equal("", r.Lies(SchachtHoehenRechnung.Deckelfeld));
        Assert.Contains("Hauptdeckel", r.Warnung);
        b.SetzeHauptdeckel(b.Projekt.Objektakten.First(a => a.Art == "deckel"));
        Assert.Equal("520.600", SchachtHoehenRechnung.Fuer(b).Lies(SchachtHoehenRechnung.Deckelfeld));
    }

    private static ObjektaktenBearbeitung Beispiel(string deckel, string sohle, string tiefe)
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Tiefe", tiefe, FieldSource.Kataster, false);
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Werte = new()
            { [SchachtHoehenRechnung.Sohlenfeld] = new() { Text = sohle } } });
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Werte = new()
            { ["deckel.hoehe"] = new() { Text = deckel } } });
        return new(p, s.Id, "schacht");
    }
}
