using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingInlineEvidencePreviewServiceTests
{
    [Fact]
    public void Build_returns_missing_state_when_preview_path_is_missing()
    {
        var state = CodingInlineEvidencePreviewService.Build(
            new CodingEvent { Entry = new ProtocolEntry() },
            _ => null,
            _ => throw new InvalidOperationException("file check should not run"),
            _ => throw new InvalidOperationException("image loader should not run"));

        Assert.Null(state.Source);
        Assert.False(state.ImageVisible);
        Assert.Equal("Kein Bild", state.StatusText);
        Assert.True(state.StatusVisible);
    }

    [Fact]
    public void Build_loads_image_when_preview_file_exists()
    {
        var image = new DrawingImage();
        string? loadedPath = null;

        var state = CodingInlineEvidencePreviewService.Build(
            new CodingEvent { Entry = new ProtocolEntry() },
            _ => @"C:\preview\markiert.png",
            path => path.EndsWith("markiert.png", StringComparison.Ordinal),
            path =>
            {
                loadedPath = path;
                return image;
            });

        Assert.Same(image, state.Source);
        Assert.True(state.ImageVisible);
        Assert.False(state.StatusVisible);
        Assert.Null(state.StatusText);
        Assert.Equal(@"C:\preview\markiert.png", loadedPath);
    }
}
