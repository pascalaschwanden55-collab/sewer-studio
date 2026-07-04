using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoPreviewRendererTests
{
    [Fact]
    public void Apply_setzt_bildquellen_und_versteckt_placeholder_fuer_vorhandene_fotos()
    {
        RunSta(() =>
        {
            var photo1Source = CreateImageSource();
            var photo2Source = CreateImageSource();
            var targets = CreateTargets(path => path == "foto1.png" ? photo1Source : photo2Source);

            VsaCodeExplorerPhotoPreviewRenderer.Apply(
                new VsaCodeExplorerPhotoPreview(
                    Photo1Path: "foto1.png",
                    ShowPhoto1Placeholder: false,
                    Photo2Path: "foto2.png",
                    ShowPhoto2Placeholder: false),
                targets);

            Assert.Same(photo1Source, targets.Photo1Image.Source);
            Assert.Same(photo2Source, targets.Photo2Image.Source);
            Assert.Equal(Visibility.Collapsed, targets.Photo1Placeholder.Visibility);
            Assert.Equal(Visibility.Collapsed, targets.Photo2Placeholder.Visibility);
        });
    }

    [Fact]
    public void Apply_leert_bildquelle_und_zeigt_placeholder_wenn_foto_fehlt()
    {
        RunSta(() =>
        {
            var oldSource = CreateImageSource();
            var targets = CreateTargets(_ => CreateImageSource());
            targets.Photo1Image.Source = oldSource;

            VsaCodeExplorerPhotoPreviewRenderer.Apply(
                new VsaCodeExplorerPhotoPreview(
                    Photo1Path: null,
                    ShowPhoto1Placeholder: true,
                    Photo2Path: null,
                    ShowPhoto2Placeholder: true),
                targets);

            Assert.Null(targets.Photo1Image.Source);
            Assert.Null(targets.Photo2Image.Source);
            Assert.Equal(Visibility.Visible, targets.Photo1Placeholder.Visibility);
            Assert.Equal(Visibility.Visible, targets.Photo2Placeholder.Visibility);
        });
    }

    [Fact]
    public void Apply_zeigt_placeholder_und_leert_bildquelle_wenn_laden_fehlschlaegt()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(_ => throw new InvalidOperationException("kaputt"));
            targets.Photo1Image.Source = CreateImageSource();

            VsaCodeExplorerPhotoPreviewRenderer.Apply(
                new VsaCodeExplorerPhotoPreview(
                    Photo1Path: "foto1.png",
                    ShowPhoto1Placeholder: false,
                    Photo2Path: null,
                    ShowPhoto2Placeholder: true),
                targets);

            Assert.Null(targets.Photo1Image.Source);
            Assert.Equal(Visibility.Visible, targets.Photo1Placeholder.Visibility);
        });
    }

    private static VsaCodeExplorerPhotoPreviewRenderTargets CreateTargets(Func<string, ImageSource> load)
        => new(
            Photo1Image: new Image(),
            Photo1Placeholder: new Border(),
            Photo2Image: new Image(),
            Photo2Placeholder: new Border(),
            LoadImageSource: load);

    private static ImageSource CreateImageSource()
        => new DrawingImage(new GeometryDrawing());

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
