using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingPointFollowUpPolicyTests
{
    private static readonly double[] Broad115 = [186.933884, 129.359619, 529.539429, 576];
    private static readonly double[] Broad120 = [72.6, 50.8, 591.9, 566.2];
    private static readonly double[] Text115 = [584.578247, 520.702942, 679.666321, 551.83606];

    [Fact]
    public void Reale_Metertextbox_blockiert_raeumlich_anderen_Rohrbefund_nicht()
    {
        var session = new CodingSession();
        var text = Add(session, Event(10.13, 115, 0.368, Text115));
        var pipe = Event(10.74, 120, 0.971, Broad120);
        Assert.False(CodingPointGeometry.Matches(text.Overlay, pipe.Overlay));
        Assert.Empty(Candidates(session, pipe));
        Assert.Equal(10.13, text.MeterAtCapture); // Kein nachtraegliches Loeschen des Fehlbefunds.
    }

    [Fact]
    public void Reale_breite_Folgebox_verbessert_Beleg_ohne_neue_ID_und_mit_flacher_History()
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.3154, Broad115));
        var id = before.EventId;
        var entryId = before.Entry.EntryId;
        var next = Event(10.74, 120, 0.9446, Broad120);
        Assert.True(Improve(session, before, next));
        Assert.Equal(id, before.EventId);
        Assert.Equal(entryId, before.Entry.EntryId);
        Assert.Equal(10.74, before.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(120), before.Entry.Zeit);
        Assert.Equal("frame-120.png", Assert.Single(before.Entry.FotoPaths));
        Assert.Equal("mask-120", before.AiContext!.SamMaskRle);
        var old = Assert.Single(before.AiContext.PreviousEvidence);
        Assert.Equal(10.13, old.MeterAtCapture);
        Assert.Equal(0.3154, old.AiContext.Evidence!.YoloConf);
        Assert.Equal("frame-115.png", Assert.Single(old.Entry.FotoPaths));
        Assert.Empty(old.AiContext.PreviousEvidence);
        var restored = JsonSerializer.Deserialize<CodingEvent>(JsonSerializer.Serialize(before))!;
        Assert.Single(restored.AiContext!.PreviousEvidence);
        // Ein geladener Altbestand bekommt keinen neuen Unberuehrtheitsnachweis.
        var reopened = new CodingSession(); reopened.Events.Add(restored);
        Assert.False(Improve(reopened, restored, Event(10.8, 125, 1, Broad120)));
    }

    [Theory]
    [InlineData(10.9, 130, 0.5, 0.90, true)] // Grenzen: 15s, +.10, SAM -.05.
    [InlineData(10.9, 130.001, 0.6, 0.95, false)]
    [InlineData(11.13, 120, 0.6, 0.95, false)] // Ein Meter ist ausgeschlossen.
    [InlineData(10.12, 120, 0.6, 0.95, false)]
    [InlineData(10.7, 114, 0.6, 0.95, false)]
    [InlineData(10.7, 120, 0.499, 0.95, false)]
    [InlineData(10.7, 120, 0.6, 0.899, false)]
    public void Zeit_Meter_Confidence_und_Maskengrenzen_sind_explizit(
        double meter, double seconds, double score, double sam, bool expected)
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.4, Broad115));
        var next = Event(meter, seconds, score, Broad115);
        next.AiContext!.Evidence!.SamMaskStability = sam;
        Assert.Equal(expected, Improve(session, before, next));
    }

    [Fact]
    public void Ursprungsanker_wandert_nach_Verbesserung_nicht_mit()
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.3, Broad115));
        Assert.True(Improve(session, before, Event(10.74, 120, 0.6, Broad120)));
        Assert.False(Improve(session, before, Event(11.2, 125, 0.9, Broad120)));
        Assert.Empty(Candidates(session, Event(11.2, 125, 0.9, Broad120)));
        Assert.False(Improve(session, before, Event(10.8, 131, 0.9, Broad120)));
        Assert.Equal(10.74, before.MeterAtCapture);
    }

    [Fact]
    public void Mehrdeutige_ueberlappende_Punkte_werden_nicht_ueberschrieben()
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.4, Broad115));
        Add(session, Event(10.2, 115, 0.3, Broad115));
        Assert.False(Improve(session, before, Event(10.74, 120, 0.9, Broad120)));
    }

    [Theory]
    [InlineData(CodingUserDecision.Accepted)]
    [InlineData(CodingUserDecision.AcceptedWithEdit)]
    [InlineData(CodingUserDecision.Rejected)]
    [InlineData(CodingUserDecision.Ignored)]
    public void Menschlich_beruehrte_Zeile_bleibt_auch_bei_Ignored_gesperrt(CodingUserDecision decision)
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.4, Broad115));
        CodingPointFollowUpPolicy.MarkHumanTouched(before);
        before.AiContext!.Decision = decision;
        var unchanged = JsonSerializer.Serialize(before);
        Assert.False(Improve(session, before, Event(10.74, 120, 0.9, Broad120)));
        Assert.Equal(unchanged, JsonSerializer.Serialize(before));
    }

    [Fact]
    public void Entfernte_verworfene_Zeile_bleibt_in_dieser_Sitzung_als_Sperrbeleg()
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.4, Broad115));
        CodingPointFollowUpPolicy.MarkHumanTouched(before);
        before.AiContext!.Decision = CodingUserDecision.Rejected;
        session.Events.Remove(before);
        var next = Event(10.74, 120, 0.9, Broad120);
        Assert.Same(before, Assert.Single(Candidates(session, next)));
        Assert.False(Improve(session, before, next));
        Assert.Empty(Candidates(new CodingSession(), next));
    }

    [Theory]
    [InlineData("description")]
    [InlineData("photo")]
    [InlineData("overlay")]
    [InlineData("confidence")]
    [InlineData("manual")]
    public void Unprotokollierte_Aenderung_verliert_den_Unberuehrtheitsnachweis(string field)
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.4, Broad115));
        switch (field)
        {
            case "description": before.Entry.Beschreibung = "Handnotiz"; break;
            case "photo": before.Entry.FotoPaths.Add("manuell.png"); break;
            case "overlay": before.Overlay!.Points[0] = new(0, 0); break;
            case "confidence": before.AiContext!.Confidence = double.NaN; break;
            case "manual": before.Entry.Source = ProtocolEntrySource.Manual; break;
        }
        Assert.False(Improve(session, before, Event(10.74, 120, 0.9, Broad120)));
    }

    [Theory]
    [InlineData("photo")]
    [InlineData("mask")]
    [InlineData("source")]
    [InlineData("meter")]
    [InlineData("red")]
    [InlineData("box")]
    [InlineData("technical")]
    [InlineData("dimensions")]
    public void Unvollstaendiger_oder_nicht_vergleichbarer_Folgebeleg_ersetzt_nichts(string fault)
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.4, Broad115));
        var next = Event(10.74, 120, 0.9, Broad120);
        switch (fault)
        {
            case "photo": next.Entry.FotoPaths.Clear(); break;
            case "mask": next.AiContext!.SamMaskRle = null; break;
            case "source": next.AiContext!.SuggestedByModelSha256 = new string('b', 64); break;
            case "red": next.AiContext!.QualityGateLevel = "Red"; break;
            case "box": next.Overlay = null; break;
            case "technical": next.AiContext!.ObservationHasTechnicalFailure = true; break;
            case "dimensions": next.AiContext!.SamMaskImageWidth = 1920; break;
        }
        Assert.False(Improve(session, before, next, fault != "meter"));
        Assert.Empty(before.AiContext!.PreviousEvidence);
    }

    [Fact]
    public void Zwei_raeumlich_getrennte_nahe_Schaeden_bleiben_getrennt()
    {
        var session = new CodingSession();
        Add(session, Event(5, 20, 0.4, [50, 100, 150, 200]));
        var next = Event(5.2, 25, 0.9, [500, 100, 600, 200]);
        Assert.Empty(Candidates(session, next));
    }

    [Fact]
    public void Mehrere_Verbesserungen_behalten_jeden_alten_Beleg_ohne_verschachtelte_Kopien()
    {
        var session = new CodingSession();
        var before = Add(session, Event(10.13, 115, 0.2, Broad115));
        Assert.True(Improve(session, before, Event(10.5, 120, 0.5, Broad115)));
        Assert.True(Improve(session, before, Event(10.8, 125, 0.8, Broad115)));
        Assert.Equal(new[] { 115d, 120d }, before.AiContext!.PreviousEvidence.Select(e => e.VideoTimestamp.TotalSeconds));
        Assert.All(before.AiContext.PreviousEvidence, e => Assert.Empty(e.AiContext.PreviousEvidence));
        Assert.Equal("frame-125.png", Assert.Single(before.Entry.FotoPaths));
    }

    [Fact]
    public void Verzoegerte_Fotoablage_erzeugt_keinen_nachtraeglichen_Unberuehrtheitsnachweis()
    {
        var session = new CodingSession();
        var before = Event(10.13, 115, 0.3, Broad115);
        before.Entry.FotoPaths.Clear();
        Add(session, before);
        before.Entry.FotoPaths.Add("late.png");
        Assert.False(Improve(session, before, Event(10.74, 120, 0.9, Broad120)));
        Assert.Empty(before.AiContext!.PreviousEvidence);
    }

    [Fact]
    public void Geschaetzter_Ursprung_und_ausgetauschte_Instanz_bleiben_ohne_Ersetzungsrecht()
    {
        var session = new CodingSession();
        var estimated = Event(10.13, 115, 0.3, Broad115);
        estimated.Entry.CodeMeta = new() { Parameters = { ["vsa.meter.quelle"] = "geschaetzt" } };
        Add(session, estimated);
        Assert.False(Improve(session, estimated, Event(10.74, 120, 0.9, Broad120)));
        session = new();
        var original = Add(session, Event(10.13, 115, 0.3, Broad115));
        var clone = JsonSerializer.Deserialize<CodingEvent>(JsonSerializer.Serialize(original))!;
        session.Events.Clear(); session.Events.Add(clone);
        Assert.False(Improve(session, clone, Event(10.74, 120, 0.9, Broad120)));
    }

    [Theory]
    [InlineData(0.10769, true)]
    [InlineData(0.10770, false)]
    public void Geometrische_Iou_Grenze_ist_unabhaengig_vom_Confidencewert(double offset, bool expected)
    {
        var a = Event(1, 1, 0.1, [0, 0, 144, 115.2]);
        var b = Event(1, 1, 1, [offset*720, 0, (offset+.2)*720, 115.2]);
        Assert.Equal(expected, CodingPointGeometry.Matches(a.Overlay, b.Overlay));
    }

    private static CodingEvent Add(CodingSession session, CodingEvent ev)
    {
        session.Events.Add(ev);
        CodingPointFollowUpPolicy.RegisterNew(session, ev, CodingPointFollowUpPolicy.Fingerprint(
            ev.Entry, ev.Overlay, ev.AiContext, ev.MeterAtCapture, ev.VideoTimestamp));
        return ev;
    }
    private static IReadOnlyList<CodingEvent> Candidates(CodingSession session, CodingEvent next)
        => CodingPointFollowUpPolicy.CoverageCandidates(session, session.Events, next.Entry.Code,
            next.MeterAtCapture, next.Overlay);
    private static bool Improve(CodingSession session, CodingEvent before, CodingEvent next, bool osd = true)
        => CodingPointFollowUpPolicy.TryImprove(session, before, next.Entry, next.Overlay, next.AiContext!, osd,
            Candidates(session, next), out _);
    private static CodingEvent Event(double meter, double seconds, double score, double[] box)
        => new()
        {
            Entry = new() { Code = "BAI", Source = ProtocolEntrySource.Ai, MeterStart = meter,
                Zeit = TimeSpan.FromSeconds(seconds), FotoPaths = [$"frame-{seconds}.png"] },
            MeterAtCapture = meter, VideoTimestamp = TimeSpan.FromSeconds(seconds),
            Overlay = new() { ToolType = OverlayToolType.Rectangle, Points = [
                new(box[0]/720, box[1]/576), new(box[2]/720, box[1]/576),
                new(box[2]/720, box[3]/576), new(box[0]/720, box[3]/576)] },
            AiContext = new() { Decision = CodingUserDecision.Ignored, SuggestedByModelId = "test",
                SuggestedByModelSha256 = new string('a', 64), Confidence = score,
                Evidence = new() { YoloConf = score, SamMaskStability = 0.95 }, QualityGateLevel = "Yellow",
                SamMaskRle = $"mask-{seconds}", SamMaskImageWidth = 720, SamMaskImageHeight = 576 }
        };
}
