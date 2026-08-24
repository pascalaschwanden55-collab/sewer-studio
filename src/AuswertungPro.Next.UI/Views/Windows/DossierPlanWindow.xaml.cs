using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Das Ergebnis der Planbearbeitung.</summary>
public sealed record DossierPlanChoice(string ImagePath, double? WidthCm);

/// <summary>
/// Dreht, schneidet und bemisst den Uebersichtsplan.
///
/// Gearbeitet wird auf der DATEI: gedreht und zugeschnitten wird sofort, in
/// einer Kopie im Dossierordner. Damit stimmt der Plan ueberall gleich — in der
/// Vorschau, im Word und in jedem PDF daraus. Ein Original ausserhalb des
/// Dossierordners bleibt unangetastet.
///
/// Der Zoom ist nur eine Sehhilfe fuer den Zuschnitt; er veraendert das Bild
/// nicht.
/// </summary>
public partial class DossierPlanWindow : Window
{
    private readonly IPlanImageAdjuster _adjuster;
    private readonly string _targetFolder;

    private string _pfad;
    private double? _breiteCm;
    private BitmapImage? _bild;

    private Point _anfang;
    private Rectangle? _rahmen;
    private bool _zieht;

    private DossierPlanWindow(
        IPlanImageAdjuster adjuster, string imagePath, string targetFolder, double? widthCm)
    {
        InitializeComponent();

        _adjuster = adjuster;
        _targetFolder = targetFolder;
        _pfad = imagePath;
        _breiteCm = widthCm;

        WidthBox.Text = (widthCm ?? 15.0).ToString("0.#", CultureInfo.CurrentCulture);
        Lade();
        ZeigeZoom();
    }

    public static DossierPlanChoice? ShowFor(
        IPlanImageAdjuster adjuster, string imagePath, string targetFolder, double? widthCm)
    {
        ArgumentNullException.ThrowIfNull(adjuster);

        if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
            return null;

        var window = new DossierPlanWindow(adjuster, imagePath, targetFolder, widthCm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
            return null;

        return new DossierPlanChoice(window._pfad, window._breiteCm);
    }

    private void Lade()
    {
        try
        {
            var bild = new BitmapImage();
            bild.BeginInit();
            bild.CacheOption = BitmapCacheOption.OnLoad;

            // Nach Drehen oder Zuschneiden hat die Datei denselben Pfad. Ohne
            // dies zeigte das Fenster weiter das Bild von vorher.
            bild.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bild.UriSource = new Uri(System.IO.Path.GetFullPath(_pfad), UriKind.Absolute);
            bild.EndInit();
            bild.Freeze();

            _bild = bild;
            PlanImage.Source = bild;
            RahmenWeg();
            SetzeBuehnengroesse();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Der Plan konnte nicht geladen werden: " + ex.Message;
        }
    }

    /// <summary>
    /// Die Buehne bekommt die Bildgroesse mal Zoom. Dadurch ist die Umrechnung
    /// von der Maus in Bildpunkte eine einzige Division — und ein Rahmen kann
    /// nicht durch eine unbekannte Skalierung danebenliegen.
    /// </summary>
    private void SetzeBuehnengroesse()
    {
        if (_bild is null)
            return;

        Bühne.Width = Math.Max(1, _bild.PixelWidth * ZoomSlider.Value);
        Bühne.Height = Math.Max(1, _bild.PixelHeight * ZoomSlider.Value);
    }

    private void ZeigeZoom()
        => ZoomText.Text = (ZoomSlider.Value * 100)
            .ToString("0", CultureInfo.InvariantCulture) + " %";

    private void OnZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Bühne is null)
            return;

        SetzeBuehnengroesse();
        RahmenWeg();
        ZeigeZoom();
    }

    // ── Rahmen ziehen ─────────────────────────────────────────────────────

    private void OnStageDown(object sender, MouseButtonEventArgs e)
    {
        if (_bild is null)
            return;

        _anfang = e.GetPosition(Bühne);
        _zieht = true;

        RahmenWeg();

        _rahmen = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xE0, 0x40, 0x40)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(0x22, 0xE0, 0x40, 0x40))
        };

        Canvas.SetLeft(_rahmen, _anfang.X);
        Canvas.SetTop(_rahmen, _anfang.Y);
        Auswahl.Children.Add(_rahmen);

        Bühne.CaptureMouse();
    }

    private void OnStageMove(object sender, MouseEventArgs e)
    {
        if (!_zieht || _rahmen is null)
            return;

        var jetzt = e.GetPosition(Bühne);

        Canvas.SetLeft(_rahmen, Math.Min(_anfang.X, jetzt.X));
        Canvas.SetTop(_rahmen, Math.Min(_anfang.Y, jetzt.Y));
        _rahmen.Width = Math.Abs(jetzt.X - _anfang.X);
        _rahmen.Height = Math.Abs(jetzt.Y - _anfang.Y);
    }

    private void OnStageUp(object sender, MouseButtonEventArgs e)
    {
        _zieht = false;
        Bühne.ReleaseMouseCapture();

        // Ein Klick ohne Ziehen ist kein Ausschnitt.
        var gross = _rahmen is not null && _rahmen.Width > 4 && _rahmen.Height > 4;
        CropButton.IsEnabled = gross;

        if (!gross)
            RahmenWeg();
    }

    private void RahmenWeg()
    {
        Auswahl.Children.Clear();
        _rahmen = null;
        CropButton.IsEnabled = false;
    }

    // ── Aktionen ──────────────────────────────────────────────────────────

    private void OnCrop(object sender, RoutedEventArgs e)
    {
        if (_rahmen is null || _bild is null)
            return;

        var zoom = ZoomSlider.Value;
        if (zoom <= 0)
            return;

        var ergebnis = _adjuster.Crop(
            _pfad,
            _targetFolder,
            (int)Math.Round(Canvas.GetLeft(_rahmen) / zoom),
            (int)Math.Round(Canvas.GetTop(_rahmen) / zoom),
            (int)Math.Round(_rahmen.Width / zoom),
            (int)Math.Round(_rahmen.Height / zoom));

        Uebernimm(ergebnis, "Plan zugeschnitten.");
    }

    private void OnRotateLeft(object sender, RoutedEventArgs e) => Drehe(270);

    private void OnRotateRight(object sender, RoutedEventArgs e) => Drehe(90);

    private void OnRotateHalf(object sender, RoutedEventArgs e) => Drehe(180);

    private void Drehe(int grad)
        => Uebernimm(_adjuster.Rotate(_pfad, _targetFolder, grad), "Plan gedreht.");

    private void Uebernimm(PlanImageResult ergebnis, string meldung)
    {
        if (!ergebnis.Success)
        {
            StatusText.Text = ergebnis.Error ?? "Der Plan konnte nicht geändert werden.";
            return;
        }

        _pfad = ergebnis.ImagePath!;
        StatusText.Text = meldung;
        Lade();
    }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var text = WidthBox.Text?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            // Leer heisst: die Breite der Vorlage.
            _breiteCm = null;
            DialogResult = true;
            return;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var cm)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out cm))
        {
            StatusText.Text = "Die Breite ist keine Zahl.";
            return;
        }

        if (cm is <= 0 or > 30)
        {
            // Mehr als 30 cm passt auf kein A4-Blatt.
            StatusText.Text = "Die Breite muss zwischen 1 und 30 cm liegen.";
            return;
        }

        _breiteCm = cm;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
