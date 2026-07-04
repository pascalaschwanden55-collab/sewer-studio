using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerPhotoPreviewRenderTargets(
    Image Photo1Image,
    UIElement Photo1Placeholder,
    Image Photo2Image,
    UIElement Photo2Placeholder,
    Func<string, ImageSource> LoadImageSource);

public static class VsaCodeExplorerPhotoPreviewRenderer
{
    public static void Apply(VsaCodeExplorerPhotoPreview preview, VsaCodeExplorerPhotoPreviewRenderTargets targets)
    {
        ApplySlot(preview.Photo1Path, preview.ShowPhoto1Placeholder, targets.Photo1Image, targets.Photo1Placeholder, targets.LoadImageSource);
        ApplySlot(preview.Photo2Path, preview.ShowPhoto2Placeholder, targets.Photo2Image, targets.Photo2Placeholder, targets.LoadImageSource);
    }

    public static ImageSource LoadBitmapImage(string photoPath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(photoPath);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelHeight = 180;
        image.EndInit();
        return image;
    }

    private static void ApplySlot(
        string? photoPath,
        bool showPlaceholder,
        Image image,
        UIElement placeholder,
        Func<string, ImageSource> loadImageSource)
    {
        if (showPlaceholder || string.IsNullOrEmpty(photoPath))
        {
            image.Source = null;
            placeholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            image.Source = loadImageSource(photoPath);
            placeholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            image.Source = null;
            placeholder.Visibility = Visibility.Visible;
        }
    }
}
