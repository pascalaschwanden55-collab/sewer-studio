using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void UpdateInlineEvidencePreview(CodingEvent ev)
    {
        try
        {
            var previewPath = CodingDefectPreviewService.BuildPreviewImagePath(ev);
            if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
            {
                ImgInlineEvidencePreview.Source = null;
                ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
                TxtInlineEvidencePreviewStatus.Text = "Kein Bild";
                TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
                return;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new System.Uri(previewPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            ImgInlineEvidencePreview.Source = image;
            ImgInlineEvidencePreview.Visibility = Visibility.Visible;
            TxtInlineEvidencePreviewStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ImgInlineEvidencePreview.Source = null;
            ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
            TxtInlineEvidencePreviewStatus.Text = "Bild nicht ladbar";
            TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
            PlayerTrace.WriteLine($"[CodingPreview] {ex.Message}");
        }
    }
}
