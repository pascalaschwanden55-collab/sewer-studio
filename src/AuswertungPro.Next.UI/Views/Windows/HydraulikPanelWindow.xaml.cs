using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class HydraulikPanelWindow : Window
{
    // Vorher feste SolidColorBrush-/FontFamily-Felder mit hartkodierten Hex-Werten (blieben im
    // Dunkelmodus falsch, da nie themeabhaengig). Farben und Schrift werden jetzt je Zeichnung
    // ueber SetResourceReference bzw. ResolveColor (fuer selbst gebaute Verlaeufe) aus den
    // Theme-Tokens gelesen, damit ein Themewechsel wirkt (Aufgabe 12, Optik-Plan 28.09.2026).

    public HydraulikPanelWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }

    public HydraulikPanelWindow(HydraulikPanelViewModel vm) : this()
    {
        DataContext = vm;
        vm.PropertyChanged += Vm_PropertyChanged;
        Loaded += (_, _) => UpdateAll(vm);

        // Die im Code gebauten Farbverlaeufe der Rohrquerschnitt-Zeichnung (Rohrwand-Schimmer,
        // Wasserfuellung) sind Momentaufnahmen aus ResolveColor — SetResourceReference greift bei
        // einem LinearGradientBrush nicht. Ein Themewechsel waehrend das Fenster offen ist, wuerde
        // sie sonst nicht nachziehen. Neu zeichnen wie RohrquerschnittControl es fuer denselben
        // Fall schon tut; statisches Event -> beim Schliessen wieder abbestellen (Fix-Runde 1,
        // Review 29.09.2026).
        void OnThemeChanged(string _) => Dispatcher.Invoke(() => UpdateAll(vm));
        ThemeManager.ThemeChanged += OnThemeChanged;

        Closed += (_, _) =>
        {
            vm.PropertyChanged -= Vm_PropertyChanged;
            ThemeManager.ThemeChanged -= OnThemeChanged;
        };
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not HydraulikPanelViewModel vm) return;

        switch (e.PropertyName)
        {
            case nameof(HydraulikPanelViewModel.Result):
            case nameof(HydraulikPanelViewModel.HasResult):
                UpdateAll(vm);
                break;
        }
    }

    private void UpdateAll(HydraulikPanelViewModel vm)
    {
        DrawPipeCrossSection(vm.Dn, Math.Min(vm.Wasserstand, vm.Dn));
        UpdateIndicators(vm);
        UpdateConditionalColors(vm);
    }

    private void UpdateIndicators(HydraulikPanelViewModel vm)
    {
        IndV.SetResourceReference(Shape.FillProperty, vm.VelocityOk ? "SuccessBrush" : "DangerBrush");
        IndTau.SetResourceReference(Shape.FillProperty, vm.ShearOk ? "SuccessBrush" : "DangerBrush");
        IndAbl.SetResourceReference(Shape.FillProperty, vm.AblagerungOk ? "SuccessBrush" : "DangerBrush");
        IndFr.SetResourceReference(Shape.FillProperty, vm.FroudeOk ? "SuccessBrush" : "WarningBrush");

        // Ablagerung border + verdict
        AblagerungBorder.SetResourceReference(Border.BorderBrushProperty, vm.AblagerungOk ? "SuccessBrush" : "DangerBrush");

        AblagerungVerdict.SetResourceReference(Border.BackgroundProperty, vm.AblagerungOk ? "SuccessSubtleBrush" : "DangerSubtleBrush");
        // Nicht SuccessTextBrush/DangerTextBrush: DangerTextBrush erreicht auf DangerSubtleBrush im
        // Hellmodus nur 3,95:1 (unter 4,5:1) — SuccessTextBrush waere zwar knapp gueltig, aber
        // unterschiedliche Tokens fuer Erfolg/Fehler auf derselben Subtle-Flaeche waeren
        // inkonsistent. TextBrush erreicht auf beiden Subtle-Flaechen in beiden Themes 9,8-14,6:1
        // (Fix-Runde 1, Review 29.09.2026).
        AblagerungVerdictText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        // Conditional result value colors
        VTeilBlock.SetResourceReference(TextBlock.ForegroundProperty, vm.VelocityOk ? "SuccessTextBrush" : "DangerTextBrush");
        TauBlock.SetResourceReference(TextBlock.ForegroundProperty, vm.ShearOk ? "SuccessTextBrush" : "DangerTextBrush");
        FrBlock.SetResourceReference(TextBlock.ForegroundProperty, vm.FroudeOk ? "TextBrush" : "DangerTextBrush");

        // Auslastung color — *TextBrush statt der Fuellfarben: derselbe Grund wie bei GetConfidenceBrush.
        AuslastungRun.SetResourceReference(TextElement.ForegroundProperty, vm.AuslastungPercent > 80 ? "DangerTextBrush" : "SuccessTextBrush");
    }

    private void UpdateConditionalColors(HydraulikPanelViewModel vm)
    {
        // Already handled in UpdateIndicators
    }

    /// <summary>Loest einen Theme-Token als Farbe auf (fuer selbst gebaute Verlaeufe); ohne Treffer gilt der Rueckfallwert.</summary>
    private Color ResolveColor(string key, Color fallback)
        => TryFindResource(key) is SolidColorBrush solid ? solid.Color : fallback;

    // ── Pipe Cross-Section Drawing ────────────────────────────

    private void DrawPipeCrossSection(double dMm, double hMm)
    {
        var canvas = PipeCrossSection;
        canvas.Children.Clear();

        const double width = 180;
        const double height = 180;
        const double r = 70;
        double cx = width / 2;
        double cy = height / 2;
        double ratio = dMm > 0 ? Math.Min(hMm / dMm, 1) : 0;
        double waterY = cy + r - ratio * 2 * r;

        // Pipe wall (outer ring) – Verlauf aus den Rand-Tokens aufgeloest (kein SetResourceReference
        // auf einem LinearGradientBrush moeglich; wird bei jeder Neuzeichnung frisch aufgeloest).
        var pipeWallGradient = new LinearGradientBrush(
            ResolveColor("BorderLightBrush", Color.FromRgb(0xC0, 0xC0, 0xC0)),
            ResolveColor("BorderBrush", Color.FromRgb(0x90, 0x90, 0x90)), 45);
        var outerRing = new Ellipse
        {
            Width = (r + 6) * 2,
            Height = (r + 6) * 2,
            Fill = pipeWallGradient,
            StrokeThickness = 1
        };
        outerRing.SetResourceReference(Shape.StrokeProperty, "BorderBrush");
        Canvas.SetLeft(outerRing, cx - r - 6);
        Canvas.SetTop(outerRing, cy - r - 6);
        canvas.Children.Add(outerRing);

        // Pipe interior
        var inner = new Ellipse
        {
            Width = r * 2,
            Height = r * 2
        };
        inner.SetResourceReference(Shape.FillProperty, "CardBrush");
        Canvas.SetLeft(inner, cx - r);
        Canvas.SetTop(inner, cy - r);
        canvas.Children.Add(inner);

        // Water level
        if (ratio > 0)
        {
            var accent = ResolveColor("AccentBrush", Color.FromRgb(0x09, 0x69, 0xDA));
            var accentHover = ResolveColor("AccentHoverBrush", Color.FromRgb(0x09, 0x69, 0xDA));
            var waterGradient = new LinearGradientBrush(
                Color.FromArgb(0xAA, accent.R, accent.G, accent.B),
                Color.FromArgb(0xDD, accentHover.R, accentHover.G, accentHover.B), 90);

            var waterRect = new System.Windows.Shapes.Rectangle
            {
                Width = r * 2,
                Height = cy + r - waterY,
                Fill = waterGradient
            };

            // Clip to circle
            var clipGeometry = new EllipseGeometry(new Point(r, r), r, r);
            waterRect.Clip = new EllipseGeometry(
                new Point(r, cy + r - waterY + r - (cy + r - waterY)), r, r);

            // Simpler approach: use a combined geometry
            var waterClip = new EllipseGeometry(new Point(cx, cy), r, r);
            var clipRect = new RectangleGeometry(new Rect(cx - r, waterY, r * 2, cy + r - waterY + 1));
            var combined = new CombinedGeometry(GeometryCombineMode.Intersect, waterClip, clipRect);

            var waterPath = new System.Windows.Shapes.Path
            {
                Data = combined,
                Fill = waterGradient
            };
            canvas.Children.Add(waterPath);

            // Water surface line (dashed)
            if (ratio < 1)
            {
                // Calculate chord width at water level
                double dy = waterY - cy;
                double halfChord = Math.Sqrt(Math.Max(0, r * r - dy * dy));

                var surfaceLine = new Line
                {
                    X1 = cx - halfChord + 3,
                    Y1 = waterY,
                    X2 = cx + halfChord - 3,
                    Y2 = waterY,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 2.0 })
                };
                surfaceLine.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
                canvas.Children.Add(surfaceLine);
            }
        }

        // DN label
        var dnLabel = new TextBlock
        {
            Text = $"DN {dMm:F0}",
            FontSize = 11
        };
        dnLabel.SetResourceReference(TextBlock.FontFamilyProperty, "FontMono");
        dnLabel.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        dnLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(dnLabel, cx - dnLabel.DesiredSize.Width / 2);
        Canvas.SetTop(dnLabel, 6);
        canvas.Children.Add(dnLabel);

        // Water height label
        if (ratio > 0)
        {
            var hLabel = new TextBlock
            {
                Text = $"h={hMm:F0} mm",
                FontSize = 11
            };
            hLabel.SetResourceReference(TextBlock.FontFamilyProperty, "FontMono");
            hLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            hLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double labelY = Math.Max(waterY + 12, cy);
            Canvas.SetLeft(hLabel, cx - hLabel.DesiredSize.Width / 2);
            Canvas.SetTop(hLabel, labelY);
            canvas.Children.Add(hLabel);
        }
    }
}

/// <summary>Inverts a boolean value for binding.</summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}

/// <summary>Converts bool to Visibility (inverted: true→Collapsed, false→Visible).</summary>
public sealed class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Two-way converter that accepts both '.' and ',' as decimal separator for double values.</summary>
public sealed class DotCommaDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return d.ToString("G", CultureInfo.InvariantCulture);
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
            return 0d;

        // Accept both '.' and ',' — normalize to '.'
        text = text.Trim().Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result)
            ? result
            : DependencyProperty.UnsetValue;
    }
}
