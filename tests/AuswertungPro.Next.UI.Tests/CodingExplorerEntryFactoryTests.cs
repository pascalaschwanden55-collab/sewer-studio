using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingExplorerEntryFactoryTests
{
    [Fact]
    public void CreateSeed_applies_overlay_and_hint_values()
    {
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points =
            [
                new NormalizedPoint(0.1, 0.1),
                new NormalizedPoint(0.9, 0.1),
                new NormalizedPoint(0.9, 0.4)
            ],
            FillPercent = 33.3
        };

        var entry = CodingExplorerEntryFactory.CreateSeed(
            overlay,
            TimeSpan.FromSeconds(9),
            suggestedCode: "BCA",
            clockPosition: "3:00");

        Assert.Equal(ProtocolEntrySource.Manual, entry.Source);
        Assert.Equal("BCA", entry.Code);
        Assert.Equal(TimeSpan.FromSeconds(9), entry.Zeit);
        Assert.Equal("3:00", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("33.3", entry.CodeMeta.Parameters["vsa.querschnitt.prozent"]);
    }

    [Fact]
    public void CreateManualFromSelected_copies_selected_values_and_uses_fallbacks()
    {
        var meta = new ProtocolEntryCodeMeta { Code = "BAB" };
        meta.Parameters["x"] = "y";
        var selected = new ProtocolEntry
        {
            Code = "BAB",
            Beschreibung = "Riss",
            MeterEnd = 3.0,
            IsStreckenschaden = true,
            CodeMeta = meta,
            FotoPaths = ["overlay.png"],
            OriginalFotoPaths = ["original.png"],
            Training = new ProtocolEntryTrainingMeta
            {
                SkipAutomaticPersistence = true,
                SkipReason = "Fotoannotation bereits separat gespeichert",
                PhotoAnnotationSampleIds = ["photo-sample-1"]
            }
        };

        var entry = CodingExplorerEntryFactory.CreateManualFromSelected(
            selected,
            fallbackMeter: 1.2,
            fallbackTime: TimeSpan.FromSeconds(4));

        Assert.Equal(ProtocolEntrySource.Manual, entry.Source);
        Assert.Equal("BAB", entry.Code);
        Assert.Equal("Riss", entry.Beschreibung);
        Assert.Equal(1.2, entry.MeterStart);
        Assert.Equal(3.0, entry.MeterEnd);
        Assert.Equal(TimeSpan.FromSeconds(4), entry.Zeit);
        Assert.True(entry.IsStreckenschaden);
        Assert.Same(meta, entry.CodeMeta);
        Assert.Equal(["overlay.png"], entry.FotoPaths);
        Assert.Equal(["original.png"], entry.OriginalFotoPaths);
        Assert.NotSame(selected.OriginalFotoPaths, entry.OriginalFotoPaths);
        Assert.NotSame(selected.Training, entry.Training);
        Assert.True(entry.Training!.SkipAutomaticPersistence);
        Assert.Equal(["photo-sample-1"], entry.Training.PhotoAnnotationSampleIds);

        selected.Training.PhotoAnnotationSampleIds.Add("photo-sample-2");
        Assert.Single(entry.Training.PhotoAnnotationSampleIds);
    }
}
