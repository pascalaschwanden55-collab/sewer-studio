using System.Linq;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class StreckenschadenTrackerTests
{
    private static StreckenschadenTracker.Observation Obs(string code, double? clock, double meter)
        => new(code, clock, meter);

    [Fact]
    public void ErsteSichtung_oeffnet_offenen_Anfang_noch_keine_Strecke()
    {
        var t = new StreckenschadenTracker();
        var actions = t.Update([Obs("BDD", 6, 2.0)], currentMeter: 2.0);

        var a = Assert.Single(actions);
        Assert.Equal(StreckenschadenTracker.SegmentActionType.Open, a.Type);
        Assert.False(a.IsConfirmedStrecke);
        Assert.Equal(1, t.OpenCount);
    }

    [Fact]
    public void GleicherCode_aehnlicheUhrlage_setzt_Strecke_fort()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BDD", 6, 2.0)], 2.0);
        var actions = t.Update([Obs("BDD", 7, 2.5)], 2.5); // 7 Uhr ~ 6 Uhr (Toleranz 2)

        var a = Assert.Single(actions);
        Assert.Equal(StreckenschadenTracker.SegmentActionType.Extend, a.Type);
        Assert.Equal(1, t.OpenCount); // kein zweiter Anfang
    }

    [Fact]
    public void Strecke_wird_ab_ueber_einem_Meter_bestaetigt()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BDD", 6, 2.0)], 2.0);
        var unter1m = t.Update([Obs("BDD", 6, 2.8)], 2.8).Single();
        Assert.False(unter1m.IsConfirmedStrecke); // 0.8 m < 1 m

        var ueber1m = t.Update([Obs("BDD", 6, 3.5)], 3.5).Single();
        Assert.True(ueber1m.IsConfirmedStrecke); // 1.5 m > 1 m
    }

    [Fact]
    public void Verschwundener_Code_wird_nach_Toleranzdistanz_geschlossen()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BDD", 6, 2.0)], 2.0);
        t.Update([Obs("BDD", 6, 3.5)], 3.5); // bestaetigt, letzte Sichtung 3.5

        // Kurze Luecke (4.0): 0.5 m <= Toleranz 1.0 -> NICHT schliessen.
        var kurz = t.Update([], currentMeter: 4.0);
        Assert.Empty(kurz);
        Assert.Equal(1, t.OpenCount);

        // Weiter weg (5.0): 1.5 m > 1.0 -> jetzt schliessen, Ende = letzte echte Sichtung 3.5.
        var actions = t.Update([], currentMeter: 5.0);
        var close = Assert.Single(actions);
        Assert.Equal(StreckenschadenTracker.SegmentActionType.Close, close.Type);
        Assert.Equal(2.0, close.StartMeter);
        Assert.Equal(3.5, close.EndMeter);
        Assert.True(close.IsConfirmedStrecke);
        Assert.Equal(0, t.OpenCount);
    }

    [Fact]
    public void Kurze_Erkennungsluecke_bleibt_dieselbe_Strecke()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BBA", 12, 5.0)], 5.0);
        // Luecke: anderer Code, BBA fehlt, aber nur 0.5 m weiter -> BBA bleibt offen.
        var lueckenActions = t.Update([Obs("BBC", 6, 5.5)], 5.5);
        Assert.DoesNotContain(lueckenActions, x => x.MainCode == "BBA");
        Assert.Contains(lueckenActions, x => x.Type == StreckenschadenTracker.SegmentActionType.Open && x.MainCode == "BBC");

        // BBA wieder da bei 6.2 -> dieselbe offene Strecke wird fortgesetzt (kein neuer Anfang).
        var wiederDa = t.Update([Obs("BBA", 12, 6.2)], 6.2);
        Assert.Contains(wiederDa, x => x.Type == StreckenschadenTracker.SegmentActionType.Extend && x.MainCode == "BBA" && x.StartMeter == 5.0);
    }

    [Fact]
    public void UnterschiedlicheUhrlage_sind_getrennte_Strecken()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BBA", 3, 5.0)], 5.0);   // rechts
        // links -> kein Match: neue Strecke wird geoeffnet. Die alte (3 Uhr) ist erst 0.5 m weg
        // (<= Toleranz) und bleibt offen. Also nur Open, beide offen.
        var actions = t.Update([Obs("BBA", 9, 5.5)], 5.5);

        Assert.Contains(actions, x => x.Type == StreckenschadenTracker.SegmentActionType.Open && x.ClockHour == 9);
        Assert.DoesNotContain(actions, x => x.Type == StreckenschadenTracker.SegmentActionType.Close);
        Assert.Equal(2, t.OpenCount); // beide Strecken (3 Uhr noch in Toleranz, 9 Uhr neu)
    }

    [Fact]
    public void Unbekannte_Uhrlage_matcht_bestehende_Strecke()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BBC", 6, 5.0)], 5.0);
        var actions = t.Update([Obs("BBC", null, 5.5)], 5.5);

        Assert.Equal(StreckenschadenTracker.SegmentActionType.Extend, actions.Single().Type);
        Assert.Equal(1, t.OpenCount);
    }

    [Fact]
    public void CloseAll_schliesst_alle_offenen_am_Endmeter()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BDD", 6, 2.0), Obs("BBA", 12, 2.0)], 2.0);
        Assert.Equal(2, t.OpenCount);

        var actions = t.CloseAll(currentMeter: 10.0);
        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(StreckenschadenTracker.SegmentActionType.Close, a.Type));
        Assert.All(actions, a => Assert.Equal(10.0, a.EndMeter));
        Assert.Equal(0, t.OpenCount);
    }

    [Fact]
    public void CloseAll_nimmt_groesseren_von_LetzterSichtung_und_Endmeter()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BDD", 6, 2.0)], 2.0);
        t.Update([Obs("BDD", 6, 8.0)], 8.0); // letzte Sichtung 8.0

        // Endmeter 5.0 < letzte Sichtung 8.0 -> Ende = 8.0 (Schaden war nachweislich bis 8.0 da).
        var close = t.CloseAll(currentMeter: 5.0).Single();
        Assert.Equal(8.0, close.EndMeter);
    }

    [Fact]
    public void ZwoelfUhr_und_EinsUhr_gelten_als_gleiche_Lage_zyklisch()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BBC", 12, 5.0)], 5.0);
        var actions = t.Update([Obs("BBC", 1, 5.5)], 5.5); // 12 und 1 -> Abstand 1 <= Toleranz

        Assert.Equal(StreckenschadenTracker.SegmentActionType.Extend, actions.Single().Type);
    }

    [Fact]
    public void ElfUhr_und_EinsUhr_zyklisch_innerhalb_Toleranz()
    {
        var t = new StreckenschadenTracker();
        t.Update([Obs("BBC", 11, 5.0)], 5.0);
        var actions = t.Update([Obs("BBC", 1, 5.5)], 5.5); // zyklischer Abstand 11<->1 = 2 <= Toleranz

        Assert.Equal(StreckenschadenTracker.SegmentActionType.Extend, actions.Single().Type);
    }
}
