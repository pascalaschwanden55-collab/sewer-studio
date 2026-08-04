using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using System.Text.Json;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolRevisionClonerTests
{
    [Fact]
    public void Legacy_json_without_original_photo_paths_remains_readable()
    {
        var entry = JsonSerializer.Deserialize<ProtocolEntry>(
            """{"Code":"BAA","FotoPaths":["legacy.png"]}""");

        Assert.NotNull(entry);
        Assert.Equal(["legacy.png"], entry!.FotoPaths);
        Assert.NotNull(entry.OriginalFotoPaths);
        Assert.Empty(entry.OriginalFotoPaths);
    }

    [Fact]
    public void CloneEntry_kopiert_anzeige_und_original_fotopfade_tief()
    {
        var source = new ProtocolEntry
        {
            FotoPaths = ["overlay.png"],
            OriginalFotoPaths = ["original.png"]
        };

        var clone = ProtocolRevisionCloner.CloneEntry(source);
        source.FotoPaths.Add("late-overlay.png");
        source.OriginalFotoPaths.Add("late-original.png");

        Assert.Equal(["overlay.png"], clone.FotoPaths);
        Assert.Equal(["original.png"], clone.OriginalFotoPaths);
        Assert.NotSame(source.FotoPaths, clone.FotoPaths);
        Assert.NotSame(source.OriginalFotoPaths, clone.OriginalFotoPaths);
    }
}
