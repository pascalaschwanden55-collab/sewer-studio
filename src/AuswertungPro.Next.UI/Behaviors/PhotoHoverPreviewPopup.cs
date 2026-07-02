using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Wiederverwendbares Vorschau-Popup fuer die Hover-Foto-Vorschau. Komplett in Code aufgebaut
/// (kein XAML), damit das Behavior es lebensdauergebunden erzeugen/entsorgen kann.
/// Zeigt ein Foto seitenverhaeltnis-treu, blendet weich ein/aus und zeigt bei mehreren Fotos
/// eine Zaehler-Badge ("2/3"). Farben ueber Theme-Keys (CardBrush/BorderBrush) via DynamicResource.
/// </summary>
public sealed class PhotoHoverPreviewPopup : Popup
{
    private readonly Grid _rootGrid;
    private readonly Border _cardBorder;
    private readonly ScaleTransform _cardScale;
    private readonly Image _image;
    private readonly Border _badgeBorder;
    private readonly TextBlock _badgeText;

    // Token gegen Animations-Race bei schnellem Rein/Raus: eine abgelaufene Schliess-Animation
    // darf das Popup nicht schliessen, wenn zwischenzeitlich neu eingeblendet wurde.
    private long _animationToken;
    private bool _isClosing;

    public PhotoHoverPreviewPopup()
    {
        AllowsTransparency = true;
        StaysOpen = true;                      // Schliessen steuert ausschliesslich das Behavior
        Placement = PlacementMode.Mouse;
        HorizontalOffset = 16;                 // Versatz weg vom Cursor -> kein MouseLeave-Flackern
        VerticalOffset = 12;
        PopupAnimation = PopupAnimation.None;   // eigene, feinere Animation

        _cardScale = new ScaleTransform(0.92, 0.92);

        _image = new Image
        {
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

        _badgeText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        _badgeBorder = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Background = new SolidColorBrush(Color.FromArgb(0xB3, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6),
            Visibility = Visibility.Collapsed,
            Child = _badgeText
        };

        var inner = new Grid();
        inner.Children.Add(_image);
        inner.Children.Add(_badgeBorder);

        _cardBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _cardScale,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.45 },
            Child = inner
        };
        _cardBorder.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        _cardBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        _rootGrid = new Grid
        {
            Margin = new Thickness(12),   // Platz fuer den Schlagschatten
            IsHitTestVisible = false,     // WICHTIG: Popup ist eigenes HWND -> sonst MouseLeave-Flacker-Schleife
            Opacity = 0d
        };
        _rootGrid.Children.Add(_cardBorder);

        Child = _rootGrid;
    }

    /// <summary>
    /// Zeigt das Foto unter <paramref name="resolvedPath"/>. Bei bereits offenem Popup nur Bildwechsel
    /// (keine erneute Einblend-Animation). Liefert false, wenn das Bild nicht geladen werden kann.
    /// </summary>
    public bool ShowPhoto(string resolvedPath, int index, int count, double maxWidth, double maxHeight)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath) || maxWidth <= 0 || maxHeight <= 0)
            return false;

        var dpiScale = TryGetDpiScale();

        BitmapImage bitmap;
        try
        {
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(resolvedPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;   // Datei sofort schliessen
            var decodeWidth = (int)Math.Ceiling(maxWidth * dpiScale);
            if (decodeWidth > 0)
                bitmap.DecodePixelWidth = decodeWidth;       // spart Speicher, scharf genug fuer die Box
            bitmap.EndInit();
            bitmap.Freeze();
        }
        catch
        {
            // Defektes/fehlendes Foto darf keinen Fehler ausloesen -> Popup bleibt zu.
            return false;
        }

        var (width, height) = PhotoHoverPreviewLogic.FitPreserveAspect(
            bitmap.PixelWidth, bitmap.PixelHeight, maxWidth, maxHeight);
        if (width <= 0 || height <= 0)
            return false;

        _image.Source = bitmap;
        _image.Width = width;
        _image.Height = height;

        if (count > 1)
        {
            _badgeText.Text = PhotoHoverPreviewLogic.CounterText(index, count);
            _badgeBorder.Visibility = Visibility.Visible;
        }
        else
        {
            _badgeBorder.Visibility = Visibility.Collapsed;
        }

        // Frisch einblenden, wenn geschlossen ODER gerade im Ausblenden (Race).
        if (!IsOpen || _isClosing)
        {
            IsOpen = true;
            AnimateIn();
        }

        return true;
    }

    /// <summary>Blendet das Popup weich aus (120 ms) und schliesst es danach; gibt den Bildspeicher frei.</summary>
    public void CloseAnimated()
    {
        if (!IsOpen)
            return;

        _isClosing = true;
        var token = ++_animationToken;

        var fade = new DoubleAnimation(0d, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            if (token != _animationToken)
                return;   // ein neuer Einblend-Lauf hat uebernommen
            IsOpen = false;
            _isClosing = false;
            _image.Source = null;
        };
        _rootGrid.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Sofort schliessen (Unloaded/Entsorgung) ohne Animation.</summary>
    public void CloseImmediate()
    {
        _animationToken++;   // laufende Completed-Handler entwerten
        _isClosing = false;
        _rootGrid.BeginAnimation(UIElement.OpacityProperty, null);
        IsOpen = false;
        _image.Source = null;
    }

    private void AnimateIn()
    {
        _animationToken++;
        _isClosing = false;

        // Laufende Animationen abraeumen, damit schnelles Rein/Raus nicht kollidiert.
        _rootGrid.BeginAnimation(UIElement.OpacityProperty, null);
        _cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        _rootGrid.Opacity = 0d;
        _cardScale.ScaleX = 0.92d;
        _cardScale.ScaleY = 0.92d;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };
        var scale = new DoubleAnimation(0.92d, 1d, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };

        _rootGrid.BeginAnimation(UIElement.OpacityProperty, fade);
        _cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        _cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private double TryGetDpiScale()
    {
        try
        {
            if (PlacementTarget is Visual visual)
                return VisualTreeHelper.GetDpi(visual).DpiScaleX;
        }
        catch
        {
            // Visual (noch) nicht in einer PresentationSource -> Fallback.
        }
        return 1.0d;
    }
}
