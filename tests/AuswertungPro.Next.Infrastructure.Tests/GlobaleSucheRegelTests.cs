using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GlobaleSucheRegelTests
{
    private static HaltungRecord H(string name, string strasse)
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.Street, strasse, FieldSource.Manual, false);
        return r;
    }
    private static SchachtRecord S(string nummer, string strasse)
    {
        var r = new SchachtRecord();
        r.Fields["Schachtnummer"] = nummer; r.Fields["Strasse"] = strasse;
        return r;
    }

    [Fact]
    public void Findet_Haltung_Schacht_und_Strasse_in_dieser_Reihenfolge()
    {
        var treffer = GlobaleSucheRegel.Suche("seiler", new[] { H("78998-79002", "Seilergasse") }, new[] { S("78998", "Seilergasse") }, s => s.Fields["Schachtnummer"]);
        Assert.Collection(treffer,
            t => { Assert.Equal(GlobaleSucheArt.Haltung, t.Art); Assert.Equal("Haltung 78998-79002 · Seilergasse", t.Text); },
            t => { Assert.Equal(GlobaleSucheArt.Schacht, t.Art); Assert.Equal("Schacht 78998 · Seilergasse", t.Text); },
            t => { Assert.Equal(GlobaleSucheArt.Strasse, t.Art); Assert.Equal("Strasse Seilergasse", t.Text); Assert.Equal("Seilergasse", t.Ziel); });
    }

    [Fact]
    public void Hoechstens_zwoelf_Treffer_und_leerer_Text_liefert_nichts()
    {
        var viele = System.Linq.Enumerable.Range(0, 30).Select(i => H($"{i}-{i + 1}", "Teststrasse")).ToList();
        Assert.Equal(12, GlobaleSucheRegel.Suche("test", viele, System.Array.Empty<SchachtRecord>(), s => "").Count);
        Assert.Empty(GlobaleSucheRegel.Suche("  ", viele, System.Array.Empty<SchachtRecord>(), s => ""));
    }
}
