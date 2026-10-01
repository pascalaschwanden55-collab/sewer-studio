using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.UI.Player;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSuggestionsOwnerTests
{
    [Fact]
    public void Waehrend_des_Durchlaufs_zeigt_der_Kopf_den_Fortschritt()
    {
        var owner = new CodingSuggestionsOwner();
        owner.BeginScan();
        owner.SetPercent(43);

        Assert.True(owner.IsScanning);
        Assert.Equal("KI prüft Video … 43 %", owner.HeaderText);
    }

    [Fact]
    public void Nach_dem_Durchlauf_zaehlt_der_Kopf_die_offenen_Vorschlaege()
    {
        var owner = new CodingSuggestionsOwner();
        owner.BeginScan();
        owner.Apply(Set(Bogen(30), Anfang(4)));

        Assert.False(owner.IsScanning);
        Assert.Equal(2, owner.Rows.Count);
        Assert.Equal("KI-VORSCHLÄGE (2)", owner.HeaderText);
        Assert.Equal(string.Empty, owner.HintText);
        Assert.Equal(new[] { 4.0, 30.0 }, owner.Rows.Select(r => r.TimeSeconds));
    }

    [Fact]
    public void Ein_ausgefallener_Teil_steht_als_Hinweis_da()
    {
        var owner = new CodingSuggestionsOwner();
        owner.Apply(new CodingSuggestionSet(
            [Anfang(4)],
            Array.Empty<MeterTrackPoint>(),
            CodingSuggestionPartState.NichtVerfuegbar("kein Arbeitspunkt"),
            CodingSuggestionPartState.Bereit));

        Assert.Equal("Bogen: kein Arbeitspunkt", owner.HintText);
    }

    [Fact]
    public void Bestaetigen_graut_aus_und_Ablehnen_entfernt()
    {
        var owner = new CodingSuggestionsOwner();
        owner.Apply(Set(Bogen(30), Anfang(4), Ende(143)));

        owner.Confirm(owner.Rows[0]);
        Assert.True(owner.Rows[0].IsConfirmed);
        Assert.Equal(3, owner.Rows.Count);
        Assert.Equal(2, owner.OpenCount);
        Assert.Equal("KI-VORSCHLÄGE (2)", owner.HeaderText);

        owner.Reject(owner.Rows[2]);
        Assert.Equal(2, owner.Rows.Count);
        Assert.Equal(1, owner.OpenCount);
    }

    [Fact]
    public void Fehler_und_Clear_raeumen_auf()
    {
        var owner = new CodingSuggestionsOwner();
        owner.BeginScan();
        owner.Fail("Sidecar nicht erreichbar");
        Assert.False(owner.IsScanning);
        Assert.Equal("KI-Vorschläge nicht verfügbar", owner.HeaderText);
        Assert.Equal("Sidecar nicht erreichbar", owner.HintText);

        owner.Apply(Set(Bogen(30)));
        owner.Clear();
        Assert.Empty(owner.Rows);
        Assert.Empty(owner.MeterTrack);
        Assert.Equal("KI-VORSCHLÄGE", owner.HeaderText);
    }

    [Fact]
    public void Die_Zeile_traegt_Text_und_Glyph_je_Art()
    {
        var zeile = new CodingSuggestionRow(Bogen(30));
        Assert.Equal("Bogen · Meter 9,42 · stark", zeile.Text);
        Assert.False(string.IsNullOrEmpty(zeile.Glyph));
        Assert.NotEqual(new CodingSuggestionRow(Anfang(4)).Glyph, new CodingSuggestionRow(Ende(143)).Glyph);
    }

    private static CodingSuggestionSet Set(params CodingSuggestion[] v)
        => new(v.OrderBy(s => s.PeakTimeSeconds).ToList(), [new MeterTrackPoint(30, 9.42, false)],
            CodingSuggestionPartState.Bereit, CodingSuggestionPartState.Bereit);

    private static CodingSuggestion Bogen(double s) => new(CodingSuggestionKind.Bogen, s, 9.42, false, 0.9, true, 0);
    private static CodingSuggestion Anfang(double s) => new(CodingSuggestionKind.Rohranfang, s, null, false, 0.97, true, 0.8545);
    private static CodingSuggestion Ende(double s) => new(CodingSuggestionKind.Rohrende, s, null, false, 0.91, true, 0.8889);
}
