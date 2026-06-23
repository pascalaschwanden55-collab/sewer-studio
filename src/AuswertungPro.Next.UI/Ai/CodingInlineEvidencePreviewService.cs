using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingInlineEvidencePreviewState(
    ImageSource? Source,
    bool ImageVisible,
    string? StatusText,
    bool StatusVisible);

public static class CodingInlineEvidencePreviewService
{
    public static CodingInlineEvidencePreviewState MissingImage { get; } =
        new(null, ImageVisible: false, "Kein Bild", StatusVisible: true);

    public static CodingInlineEvidencePreviewState LoadFailed { get; } =
        new(null, ImageVisible: false, "Bild nicht ladbar", StatusVisible: true);

    public static CodingInlineEvidencePreviewState Build(
        CodingEvent codingEvent,
        Func<CodingEvent, string?>? previewPathBuilder = null,
        Func<string, bool>? fileExists = null,
        Func<string, ImageSource>? imageLoader = null)
    {
        previewPathBuilder ??= ev => CodingDefectPreviewService.BuildPreviewImagePath(ev);
        fileExists ??= File.Exists;
        imageLoader ??= LoadImage;

        var previewPath = previewPathBuilder(codingEvent);
        if (string.IsNullOrWhiteSpace(previewPath) || !fileExists(previewPath))
            return MissingImage;

        return new CodingInlineEvidencePreviewState(
            imageLoader(previewPath),
            ImageVisible: true,
            StatusText: null,
            StatusVisible: false);
    }

    private static ImageSource LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
