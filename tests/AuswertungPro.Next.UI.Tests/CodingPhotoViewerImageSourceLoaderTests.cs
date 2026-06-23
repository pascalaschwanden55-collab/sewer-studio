using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoViewerImageSourceLoaderTests
{
    [Fact]
    public void Load_puts_evidence_preview_first_resolves_relative_paths_and_skips_load_failures()
    {
        var evidence = new DrawingImage();
        var photo = new DrawingImage();
        var loadedPaths = new List<string>();
        var codingEvent = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                FotoPaths = [@"Fotos\a.png", @"Fotos\broken.png"]
            }
        };

        var sources = CodingPhotoViewerImageSourceLoader.Load(
            codingEvent,
            projectFolder: @"C:\project",
            previewPathBuilder: _ => @"C:\project\evidence.png",
            fileExists: path =>
                path == @"C:\project\evidence.png"
                || path == @"C:\project\Fotos\a.png"
                || path == @"C:\project\Fotos\broken.png",
            imageLoader: path =>
            {
                loadedPaths.Add(path);
                if (path.EndsWith("broken.png", StringComparison.Ordinal))
                    throw new InvalidOperationException("cannot load");

                return path.EndsWith("evidence.png", StringComparison.Ordinal)
                    ? evidence
                    : photo;
            });

        Assert.Equal([evidence, photo], sources);
        Assert.Equal(
            [@"C:\project\evidence.png", @"C:\project\Fotos\a.png", @"C:\project\Fotos\broken.png"],
            loadedPaths);
    }
}
