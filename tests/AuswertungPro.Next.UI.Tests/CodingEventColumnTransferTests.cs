using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventColumnTransferTests
{
    private static CodingEvent Ev(string code, double meter)
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry
            {
                Code = code,
                Beschreibung = code + " Text",
                FotoPaths = { "overlay.png" },
                OriginalFotoPaths = { "original.png" }
            }
        };

    [Fact]
    public void Move_entfernt_aus_quelle_und_fuegt_sortiert_in_ziel()
    {
        var src = new ObservableCollection<CodingEvent> { Ev("BCD", 5) };
        var target = new ObservableCollection<CodingEvent> { Ev("BAB", 2), Ev("BBA", 9) };
        var moved = src[0];

        var result = CodingEventColumnTransfer.Move(moved, src, target);

        Assert.Same(moved, result);
        Assert.Empty(src);
        Assert.Equal(3, target.Count);
        Assert.Equal(new[] { 2.0, 5.0, 9.0 }, target.Select(e => e.MeterAtCapture)); // nach Meter sortiert
    }

    [Fact]
    public void Copy_laesst_original_und_dupliziert_mit_neuen_ids()
    {
        var src = new ObservableCollection<CodingEvent> { Ev("BCD", 5) };
        var target = new ObservableCollection<CodingEvent>();
        var original = src[0];

        var copy = CodingEventColumnTransfer.Copy(original, target);

        Assert.Single(src);                                   // Original bleibt
        Assert.Single(target);
        Assert.NotEqual(original.EventId, copy.EventId);       // neue EventId
        Assert.NotEqual(original.Entry.EntryId, copy.Entry.EntryId); // neue EntryId
        Assert.Equal("BCD", copy.Entry.Code);
        Assert.Equal(original.Entry.FotoPaths, copy.Entry.FotoPaths);
        Assert.NotSame(original.Entry.FotoPaths, copy.Entry.FotoPaths); // eigene Liste
        Assert.Equal(original.Entry.OriginalFotoPaths, copy.Entry.OriginalFotoPaths);
        Assert.NotSame(original.Entry.OriginalFotoPaths, copy.Entry.OriginalFotoPaths);
    }

    [Fact]
    public void CloneWithNewIds_klont_overlay_codemeta_aicontext_unabhaengig()
    {
        var original = new CodingEvent
        {
            MeterAtCapture = 5,
            Entry = new ProtocolEntry
            {
                Code = "BAB",
                Beschreibung = "Riss laengs",
                CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = "BAB",
                    Parameters = { ["Uhrlage"] = "12" },
                },
                Training = new ProtocolEntryTrainingMeta
                {
                    SkipAutomaticPersistence = true,
                    PhotoAnnotationSampleIds = ["photo-sample-1"]
                }
            },
            Overlay = new OverlayGeometry
            {
                Points = { new NormalizedPoint { X = 0.1, Y = 0.2 } },
                SamMask = new OverlaySamMask
                {
                    MaskRle = "0,10,5,85",
                    ImageWidth = 10,
                    ImageHeight = 10,
                    MaskAreaPixels = 5,
                    Confidence = 0.9,
                    Label = "manuell"
                }
            },
            AiContext = new CodingEventAiContext { SuggestedCode = "BAB", Confidence = 0.9 },
        };

        var clone = CodingEventColumnTransfer.CloneWithNewIds(original);

        // Overlay: neue GeometryId + eigene Points-Liste (Werte gleich, Referenz verschieden).
        Assert.NotEqual(original.Overlay!.GeometryId, clone.Overlay!.GeometryId);
        Assert.NotSame(original.Overlay.Points, clone.Overlay.Points);
        Assert.Equal(0.1, clone.Overlay.Points[0].X);
        Assert.NotSame(original.Overlay.SamMask, clone.Overlay.SamMask);
        Assert.Equal("0,10,5,85", clone.Overlay.SamMask!.MaskRle);
        // CodeMeta: eigenes Parameter-Dictionary (Wert gleich, Referenz verschieden).
        Assert.NotSame(original.Entry.CodeMeta!.Parameters, clone.Entry.CodeMeta!.Parameters);
        Assert.Equal("12", clone.Entry.CodeMeta.Parameters["Uhrlage"]);
        Assert.NotSame(original.Entry.Training, clone.Entry.Training);
        Assert.True(clone.Entry.Training!.SkipAutomaticPersistence);
        Assert.Equal(["photo-sample-1"], clone.Entry.Training.PhotoAnnotationSampleIds);
        // AiContext: eigene Instanz (Wert kopiert).
        Assert.NotSame(original.AiContext, clone.AiContext);
        Assert.Equal("BAB", clone.AiContext!.SuggestedCode);

        // Gegenprobe: Mutation an der Kopie faerbt nicht auf das Original ab.
        clone.Overlay.Points[0].X = 0.99;
        clone.Overlay.SamMask!.MaskRle = "0,100";
        clone.Entry.CodeMeta.Parameters["Uhrlage"] = "6";
        clone.Entry.Training.PhotoAnnotationSampleIds.Add("photo-sample-2");
        Assert.Equal(0.1, original.Overlay.Points[0].X);
        Assert.Equal("0,10,5,85", original.Overlay.SamMask!.MaskRle);
        Assert.Equal("12", original.Entry.CodeMeta.Parameters["Uhrlage"]);
        Assert.Single(original.Entry.Training!.PhotoAnnotationSampleIds);
    }
}
