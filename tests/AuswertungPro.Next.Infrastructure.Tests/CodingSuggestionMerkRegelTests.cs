using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle F4: Nur ein Durchlauf, bei dem wirklich etwas gelaufen ist, gehoert ins
/// Sitzungsregister. Ein abgeschalteter Vorabdurchlauf taeuschte in der Uebersicht sonst Arbeit
/// vor, die nie stattgefunden hat.
/// </summary>
public sealed class CodingSuggestionMerkRegelTests
{
    private static CodingSuggestionSet Set(
        CodingSuggestionPartState bogen,
        CodingSuggestionPartState enden,
        params CodingSuggestion[] vorschlaege)
        => new(vorschlaege, Array.Empty<MeterTrackPoint>(), bogen, enden);

    private static CodingSuggestion Bogen()
        => new(CodingSuggestionKind.Bogen, 12.0, 4.2, false, 0.8, true, 0.0);

    [Fact]
    public void Ein_abgeschalteter_Durchlauf_wird_nicht_gemerkt()
        => Assert.False(CodingSuggestionMerkRegel.SollMerken(CodingSuggestionSet.Leer("In den Einstellungen ausgeschaltet.")));

    [Fact]
    public void Ohne_Set_wird_nichts_gemerkt()
        => Assert.False(CodingSuggestionMerkRegel.SollMerken(null));

    [Fact]
    public void Ein_leerer_aber_gelaufener_Durchlauf_wird_gemerkt()
    {
        var set = Set(CodingSuggestionPartState.Bereit, CodingSuggestionPartState.NichtVerfuegbar("kein Arbeitspunkt"));
        Assert.True(CodingSuggestionMerkRegel.SollMerken(set));
    }

    [Fact]
    public void Ein_technischer_Fehler_ist_ein_Durchlauf_und_wird_gemerkt()
    {
        var set = Set(CodingSuggestionPartState.Fehler("Sidecar antwortet nicht"), CodingSuggestionPartState.NichtVerfuegbar("dito"));
        Assert.True(CodingSuggestionMerkRegel.SollMerken(set));
    }

    [Fact]
    public void Mit_Vorschlaegen_gibt_es_keinen_Hinweis()
    {
        var set = Set(CodingSuggestionPartState.Bereit, CodingSuggestionPartState.Bereit, Bogen());
        Assert.Equal(string.Empty, CodingSuggestionMerkRegel.Hinweis(set));
    }

    [Fact]
    public void Ohne_Fund_und_ohne_Stoerung_heisst_es_keine_Vorschlaege()
    {
        var set = Set(CodingSuggestionPartState.Bereit, CodingSuggestionPartState.Bereit);
        Assert.Equal("keine Vorschläge", CodingSuggestionMerkRegel.Hinweis(set));
    }

    [Fact]
    public void Ein_Fehler_steht_namentlich_im_Hinweis()
    {
        var set = Set(CodingSuggestionPartState.Fehler("Sidecar antwortet nicht"), CodingSuggestionPartState.Bereit);
        var hinweis = CodingSuggestionMerkRegel.Hinweis(set);

        Assert.Contains("Bogen: Fehler", hinweis, StringComparison.Ordinal);
        Assert.Contains("Sidecar antwortet nicht", hinweis, StringComparison.Ordinal);
    }
}
