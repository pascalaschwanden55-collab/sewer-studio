using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingPhotoViewerImageSourceLoader
{
    public static IReadOnlyList<ImageSource> Load(
        CodingEvent codingEvent,
        string projectFolder,
        Func<CodingEvent, string?>? previewPathBuilder = null,
        Func<string, bool>? fileExists = null,
        Func<string, ImageSource>? imageLoader = null)
    {
        previewPathBuilder ??= ev => CodingDefectPreviewService.BuildPreviewImagePath(ev);
        fileExists ??= File.Exists;
        imageLoader ??= LoadImage;

        var evidencePreviewPath = previewPathBuilder(codingEvent);
        var displayPhotoPaths = CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths(
            evidencePreviewPath,
            codingEvent.Entry.FotoPaths,
            fileExists);

        var sources = new List<ImageSource>();
        foreach (var fotoPath in displayPhotoPaths)
        {
            var resolved = CodingPhotoDisplayPathPolicy.ResolveExistingPath(
                fotoPath,
                projectFolder,
                fileExists);

            if (resolved == null)
                continue;

            try
            {
                sources.Add(imageLoader(resolved));
            }
            catch
            {
                // Einzelne defekte Fotos duerfen den Viewer nicht verhindern.
            }
        }

        return sources;
    }

    private static ImageSource LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelHeight = 360;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
