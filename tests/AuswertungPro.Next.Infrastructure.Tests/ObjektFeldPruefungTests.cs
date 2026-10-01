using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ObjektFeldPruefungTests
{
    [Fact]
    public void Belegte_Pflichtregel_wird_aus_dem_Katalog_gelesen_und_vor_dem_Schreiben_geprueft()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        var a = b.Neu("bauwerksteil");
        var feld = FieldCatalog.Objektfelder.Feld("bauwerksteil.bezeichnung");
        Assert.True(feld.WebgisPflicht);
        b.Schreibe(a, feld, "", "Einstieg 1");
        Assert.Throws<InvalidOperationException>(() => b.Schreibe(a, feld, "Einstieg 1", ""));
        Assert.Equal("Einstieg 1", b.Lies(a, feld));
    }

    [Theory]
    [InlineData("datum", "31.02.2026", false)]
    [InlineData("datum", "12.09.2026", true)]
    [InlineData("datum", "20260912", true)]
    [InlineData("zahl", "NaN", false)]
    [InlineData("zahl", "unbekannt", false)]
    [InlineData("zahl", "12,50", true)]
    [InlineData("zahl", "", true)]
    public void Belegte_Feldarten_pruefen_nur_gueltige_Eingaben(string art, string text, bool ok)
    {
        var feld = new ObjektFeldDefinition { Label = "Wert", WebgisFeldart = art };
        if (ok) ObjektFeldPruefung.Pruefe(feld, text);
        else Assert.Throws<InvalidOperationException>(() => ObjektFeldPruefung.Pruefe(feld, text));
    }
}
