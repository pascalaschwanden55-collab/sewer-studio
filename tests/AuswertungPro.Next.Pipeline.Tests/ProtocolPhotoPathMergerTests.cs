using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPhotoPathMergerTests
{
    [Fact]
    public void MergePhotoPaths_adds_missing_source_photos()
    {
        var current = new ProtocolEntry();
        var source = new ProtocolEntry();
        source.FotoPaths.Add("Fotos/Haltungen/H1/start.jpg");

        var added = ProtocolPhotoPathMerger.MergePhotoPaths(current, source);

        Assert.Equal(1, added);
        Assert.Equal(new[] { "Fotos/Haltungen/H1/start.jpg" }, current.FotoPaths);
    }

    [Fact]
    public void MergePhotoPaths_skips_stale_import_duplicate_when_holding_photo_was_renamed()
    {
        var current = new ProtocolEntry();
        current.FotoPaths.Add("Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg");

        var added = ProtocolPhotoPathMerger.MergePhotoPaths(
            current,
            new[] { @"D:\Projekt\Importdateien\XTF\Foto\H_22147-547.01_116.jpg" });

        Assert.Equal(0, added);
        Assert.Equal(new[] { "Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg" }, current.FotoPaths);
    }

}
