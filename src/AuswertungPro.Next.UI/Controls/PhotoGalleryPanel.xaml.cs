using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Foto-Galerie fuer das Haltungs-Detail: zeigt alle Schadensfotos einer
/// Haltung als Thumbnail-Raster (Quelle: HaltungFotoGalerieBuilder).
/// Klick oeffnet das Foto im Standard-Bildbetrachter.
/// </summary>
public partial class PhotoGalleryPanel : UserControl
{
    public static readonly DependencyProperty TileSizeProperty =
        DependencyProperty.Register(
            nameof(TileSize),
            typeof(double),
            typeof(PhotoGalleryPanel),
            new FrameworkPropertyMetadata(124d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTileSizeChanged));

    private readonly AppSettings _settings;
    private bool _isLoadedFromSettings;

    public PhotoGalleryPanel()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        PhotoHoverPreviewBehavior.SetPhotoPathsSelector(FotoListe, PhotoHoverPreviewSelectors.GalerieFotoPhotos);
        TileSize = ClampTileSize(_settings.PhotoGalleryTileSize);
        _isLoadedFromSettings = true;
    }

    /// <summary>Anzahl der aktuell angezeigten Fotos (fuer den Expander-Header).</summary>
    public int FotoCount { get; private set; }

    public double TileSize
    {
        get => (double)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    public void Update(IReadOnlyList<GalerieFoto> fotos)
    {
        FotoListe.ItemsSource = fotos;
        FotoCount = fotos.Count;
    }

    private void Foto_Click(object sender, MouseButtonEventArgs e)
    {
        _ = e;
        if ((sender as FrameworkElement)?.DataContext is GalerieFoto foto)
            DataPageOriginalPdfController.TryShellOpen(foto.Pfad);
    }

    private void Detach_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        var fotos = FotoListe.ItemsSource as IReadOnlyList<GalerieFoto>
            ?? FotoListe.Items.OfType<GalerieFoto>().ToList();

        var panel = new PhotoGalleryPanel
        {
            Margin = new Thickness(12),
            TileSize = TileSize
        };
        panel.Update(fotos);

        var window = new Window
        {
            Title = "Foto-Galerie",
            Owner = Window.GetWindow(this),
            Width = 760,
            Height = 520,
            MinWidth = 420,
            MinHeight = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        WindowStateManager.Track(window, "PhotoGalleryWindow");
        window.Show();
    }

    private static void OnTileSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PhotoGalleryPanel panel || e.NewValue is not double value)
            return;

        var clamped = ClampTileSize(value);
        if (Math.Abs(clamped - value) > 0.01)
        {
            panel.TileSize = clamped;
            return;
        }

        if (!panel._isLoadedFromSettings)
            return;

        panel._settings.PhotoGalleryTileSize = clamped;
        panel._settings.Save();
    }

    private static double ClampTileSize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 80d, 260d) : 124d;
}

/// <summary>
/// Laedt Thumbnails speicherschonend (DecodePixelWidth statt Vollbild).
/// Fehlerhafte/fehlende Dateien liefern null statt einer Exception.
/// </summary>
public sealed class GalerieThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string pfad || string.IsNullOrWhiteSpace(pfad))
            return null;

        try
        {
            var bild = new BitmapImage();
            bild.BeginInit();
            bild.UriSource = new Uri(pfad, UriKind.Absolute);
            bild.DecodePixelWidth = 200;
            bild.CacheOption = BitmapCacheOption.OnLoad;
            bild.EndInit();
            bild.Freeze();
            return bild;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class GalerieTileImageHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var size = value is double d && double.IsFinite(d) ? Math.Clamp(d, 80d, 260d) : 124d;
        return Math.Round(size * 0.66d);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
