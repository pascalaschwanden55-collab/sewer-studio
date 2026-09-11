using System;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CodingSuggestionRegistryTests
{
    private static readonly Guid ProjektA = Guid.NewGuid();
    private static readonly Guid ProjektB = Guid.NewGuid();

    [Fact]
    public void Merkt_je_Haltung_nur_den_letzten_Lauf_juengster_zuerst()
    {
        var reg = new CodingSuggestionRegistry();
        var n = 0; reg.Geaendert += () => n++;
        reg.Merke(ProjektA, "1-2", CodingSuggestionSet.Leer("a"));
        reg.Merke(ProjektA, "3-4", CodingSuggestionSet.Leer("b"));
        reg.Merke(ProjektA, "1-2", CodingSuggestionSet.Leer("c"));
        var heute = reg.Heute(ProjektA);
        Assert.Equal(2, heute.Count);
        Assert.Equal("1-2", heute[0].Haltung);
        Assert.Equal("c", heute[0].Set.BogenTeil.Grund);
        Assert.Equal(3, n);
    }

    [Fact]
    public void Leere_Haltung_wird_nicht_gemerkt()
    {
        var reg = new CodingSuggestionRegistry();
        reg.Merke(ProjektA, " ", CodingSuggestionSet.Leer("a"));
        Assert.Empty(reg.Heute(ProjektA));
    }

    [Fact]
    public void Ein_werfender_Abonnent_stoppt_weder_Merke_noch_die_uebrigen_Abonnenten()
    {
        var reg = new CodingSuggestionRegistry();
        var zweiterAufgerufen = false;
        reg.Geaendert += () => throw new InvalidOperationException("kaputter Abonnent");
        reg.Geaendert += () => zweiterAufgerufen = true;

        var ausnahme = Record.Exception(() => reg.Merke(ProjektA, "1-2", CodingSuggestionSet.Leer("a")));

        Assert.Null(ausnahme);
        Assert.True(zweiterAufgerufen);
        Assert.Single(reg.Heute(ProjektA));
    }

    /// <summary>
    /// R4 (Gesamtaudit 08.09.2026): Ein Lauf gehoert zu genau dem Projekt, in dem er
    /// entstanden ist. Vorher schluesselte das Register nur nach Haltungsname; die
    /// Uebersicht von Projekt B zeigte danach noch den Lauf aus Projekt A.
    /// </summary>
    [Fact]
    public void Ein_Lauf_erscheint_nicht_in_einem_anderen_Projekt()
    {
        var reg = new CodingSuggestionRegistry();
        reg.Merke(ProjektA, "nur-in-Projekt-A", CodingSuggestionSet.Leer("a"));

        Assert.Single(reg.Heute(ProjektA));
        Assert.Empty(reg.Heute(ProjektB));
    }

    /// <summary>
    /// R4: Gleiche Haltungsnamen kommen in verschiedenen Projekten vor. Sie duerfen sich
    /// nicht gegenseitig ueberschreiben.
    /// </summary>
    [Fact]
    public void Gleiche_Haltungsnamen_zweier_Projekte_bleiben_getrennt()
    {
        var reg = new CodingSuggestionRegistry();
        reg.Merke(ProjektA, "1-2", CodingSuggestionSet.Leer("a"));
        reg.Merke(ProjektB, "1-2", CodingSuggestionSet.Leer("b"));

        Assert.Equal("a", Assert.Single(reg.Heute(ProjektA)).Set.BogenTeil.Grund);
        Assert.Equal("b", Assert.Single(reg.Heute(ProjektB)).Set.BogenTeil.Grund);
    }

    /// <summary>
    /// R4: Ohne bekanntes Projekt wird nichts gemerkt — sonst waere der Lauf wieder in
    /// jedem Projekt sichtbar. Dieselbe Regel wie bei der leeren Haltung.
    /// </summary>
    [Fact]
    public void Ohne_Projekt_wird_nichts_gemerkt()
    {
        var reg = new CodingSuggestionRegistry();
        reg.Merke(Guid.Empty, "1-2", CodingSuggestionSet.Leer("a"));

        Assert.Empty(reg.Heute(Guid.Empty));
        Assert.Empty(reg.Heute(ProjektA));
    }
}
