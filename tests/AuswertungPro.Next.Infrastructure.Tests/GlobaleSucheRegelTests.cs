using System.Collections.Generic;
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

    private static GlobaleSucheBefehlEintrag Befehl(string anzeigename, bool verfuegbar = true, System.Action? aktion = null)
        => new(anzeigename, anzeigename, "", verfuegbar, aktion ?? (() => { }));

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

    // --- Aufgabe 14: Befehle in der Strg+K-Suche ------------------------------------------

    [Fact]
    public void Ein_passender_Befehl_wird_gefunden_und_seine_Aktion_ist_ausfuehrbar()
    {
        var ausgefuehrt = false;
        var befehle = new[] { Befehl("Neues Projekt", aktion: () => ausgefuehrt = true) };

        var treffer = GlobaleSucheRegel.Suche("neues", System.Array.Empty<HaltungRecord>(), System.Array.Empty<SchachtRecord>(), s => "", befehle);

        var befehlstreffer = Assert.Single(treffer);
        Assert.Equal(GlobaleSucheArt.Befehl, befehlstreffer.Art);
        Assert.Equal("Neues Projekt", befehlstreffer.Text);
        Assert.Equal("", befehlstreffer.Glyph);
        Assert.IsType<System.Action>(befehlstreffer.Ziel);
        ((System.Action)befehlstreffer.Ziel!)();
        Assert.True(ausgefuehrt);
    }

    [Fact]
    public void Befehlssuche_ist_umlaut_tolerant_wie_die_Einstellungssuche()
    {
        var befehle = new[] { Befehl("Über SewerStudio") };

        // "ueber" faltet auf dasselbe wie "Über" (ae/oe/ue-Schreibweise, SucheTextFaltung).
        var treffer = GlobaleSucheRegel.Suche("ueber", System.Array.Empty<HaltungRecord>(), System.Array.Empty<SchachtRecord>(), s => "", befehle);

        Assert.Single(treffer);
    }

    [Fact]
    public void Ein_nicht_verfuegbarer_Befehl_bleibt_unsichtbar_wenn_kein_Projekt_offen_ist()
    {
        // Verfuegbar=false spiegelt hier z. B. NavItem.IsAvailable/CanExecute ohne offenes
        // Projekt — die Regel filtert, statt den Befehl nur zu deaktivieren.
        var befehle = new[] { Befehl("Gehe zu: Haltungen", verfuegbar: false) };

        var treffer = GlobaleSucheRegel.Suche("haltungen", System.Array.Empty<HaltungRecord>(), System.Array.Empty<SchachtRecord>(), s => "", befehle);

        Assert.Empty(treffer);
    }

    [Fact]
    public void Bei_einer_Zahl_im_Suchtext_bleiben_Datentreffer_vor_Befehlen()
    {
        var haltung = H("AB100-100", "Teststrasse");
        var befehle = new[] { Befehl("AB1 Werkzeug") };

        var treffer = GlobaleSucheRegel.Suche("ab1", new[] { haltung }, System.Array.Empty<SchachtRecord>(), s => "", befehle);

        Assert.Collection(treffer,
            t => Assert.Equal(GlobaleSucheArt.Haltung, t.Art),
            t => Assert.Equal(GlobaleSucheArt.Befehl, t.Art));
    }

    [Fact]
    public void Bei_reinem_Wort_im_Suchtext_gehen_gut_treffende_Befehle_den_Datentreffern_voran()
    {
        var haltung = H("AB100-100", "Teststrasse");
        var befehle = new[] { Befehl("AB1 Werkzeug") };

        // Dieselben zwei Treffer wie oben, aber ohne Ziffer im Suchtext — jetzt zuerst der Befehl.
        var treffer = GlobaleSucheRegel.Suche("ab", new[] { haltung }, System.Array.Empty<SchachtRecord>(), s => "", befehle);

        Assert.Collection(treffer,
            t => Assert.Equal(GlobaleSucheArt.Befehl, t.Art),
            t => Assert.Equal(GlobaleSucheArt.Haltung, t.Art));
    }

    [Fact]
    public void Hoechstens_sechs_Befehlstreffer()
    {
        var viele = new List<GlobaleSucheBefehlEintrag>();
        for (var i = 0; i < 10; i++)
            viele.Add(Befehl($"Testbefehl {i}"));

        var treffer = GlobaleSucheRegel.Suche("testbefehl", System.Array.Empty<HaltungRecord>(), System.Array.Empty<SchachtRecord>(), s => "", viele);

        Assert.Equal(6, treffer.Count);
    }

    [Fact]
    public void Ohne_Befehlskatalog_bleibt_das_Verhalten_wie_vor_Aufgabe_14()
    {
        var treffer = GlobaleSucheRegel.Suche("78998-79002", new[] { H("78998-79002", "Seilergasse") }, System.Array.Empty<SchachtRecord>(), s => "");

        var einziger = Assert.Single(treffer);
        Assert.Equal(GlobaleSucheArt.Haltung, einziger.Art);
    }
}
