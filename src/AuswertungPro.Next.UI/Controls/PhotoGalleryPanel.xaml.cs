using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Foto-Galerie fuer das Haltungs-Detail: zeigt alle Schadensfotos einer
/// Haltung als Thumbnail-Raster (Quelle: HaltungFotoGalerieBuilder).
/// Klick oeffnet das Foto im Standard-Bildbetrachter.
/// </summary>
public partial class PhotoGalleryPanel : UserControl
{
    public PhotoGalleryPanel()
    {
        InitializeComponent();
    }

    /// <summary>Anzahl der aktuell angezeigten Fotos (fuer den Expander-Header).</summary>
    public int FotoCount { get; private set; }

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
