using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class HydraulikPanelWindow : Window
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x1A, 0x7F, 0x37));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xCF, 0x22, 0x2E));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(0x9A, 0x67, 0x00));
    private static readonly SolidColorBrush DimBrush = new(Color.FromRgb(0x8C, 0x95, 0x9F));
    private static readonly SolidColorBrush WarnValueBrush = new(Color.FromRgb(0xCF, 0x22, 0x2E));
    private static readonly SolidColorBrush GoodValueBrush = new(Color.FromRgb(0x1A, 0x7F, 0x37));
    private static readonly SolidColorBrush NormalValueBrush = new(Color.FromRgb(0x1F, 0x23, 0x28));
    private static readonly SolidColorBrush GreenBgBrush = new(Color.FromArgb(0x20, 0x1A, 0x7F, 0x37));
    private static readonly SolidColorBrush RedBgBrush = new(Color.FromArgb(0x20, 0xCF, 0x22, 0x2E));
    private static readonly SolidColorBrush PipeWallStroke = new(Color.FromRgb(0x70, 0x70, 0x70));
    private static readonly SolidColorBrush PipeInteriorBrush = new(Color.FromRgb(0xF0, 0xF0, 0xF0));
    private static readonly SolidColorBrush WaterLineBrush = new(Color.FromRgb(0x09, 0x69, 0xDA));
    private static readonly SolidColorBrush LabelDimBrush = new(Color.FromRgb(0x57, 0x60, 0x6A));
    private static readonly SolidColorBrush LabelDarkBrush = new(Color.FromRgb(0x1F, 0x23, 0x28));
    private static readonly FontFamily ConsolasFont = new("Consolas");
    private static readonly LinearGradientBrush PipeWallGradient = new(
        Color.FromRgb(0xC0, 0xC0, 0xC0), Color.FromRgb(0x90, 0x90, 0x90), 45);
    private static readonly LinearGradientBrush WaterGradient = new(
        Color.FromArgb(0xAA, 0x54, 0xAE, 0xFF), Color.FromArgb(0xDD, 0x09, 0x69, 0xDA), 90);

    static HydraulikPanelWindow()
    {
        GreenBrush.Freeze(); RedBrush.Freeze(); YellowBrush.Freeze(); DimBrush.Freeze();
        WarnValueBrush.Freeze(); GoodValueBrush.Freeze(); NormalValueBrush.Freeze();
        GreenBgBrush.Freeze(); RedBgBrush.Freeze();
        PipeWallStroke.Freeze(); PipeInteriorBrush.Freeze(); WaterLineBrush.Freeze();
        LabelDimBrush.Freeze(); LabelDarkBrush.Freeze();
        PipeWallGradient.Freeze(); WaterGradient.Freeze();
    }

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
        Closed += (_, _) => vm.PropertyChanged -= Vm_PropertyChanged;
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
        IndV.Fill = vm.VelocityOk ? GreenBrush : RedBrush;
        IndTau.Fill = vm.ShearOk ? GreenBrush : RedBrush;
        IndAbl.Fill = vm.AblagerungOk ? GreenBrush : RedBrush;
        IndFr.Fill = vm.FroudeOk ? GreenBrush : YellowBrush;

        // Ablagerung border + verdict
        AblagerungBorder.BorderBrush = vm.AblagerungOk ? GreenBrush : RedBrush;

        AblagerungVerdict.Background = vm.AblagerungOk ? GreenBgBrush : RedBgBrush;
        AblagerungVerdictText.Foreground = vm.AblagerungOk ? GreenBrush : RedBrush;

        // Conditional result value colors
        VTeilBlock.Foreground = vm.VelocityOk ? GoodValueBrush : WarnValueBrush;
        TauBlock.Foreground = vm.ShearOk ? GoodValueBrush : WarnValueBrush;
        FrBlock.Foreground = vm.FroudeOk ? NormalValueBrush : WarnValueBrush;

        // Auslastung color
        AuslastungRun.Foreground = vm.AuslastungPercent > 80 ? RedBrush : GreenBrush;
    }

    private void UpdateConditionalColors(HydraulikPanelViewModel vm)
    {
        // Already handled in UpdateIndicators
    }

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

        // Pipe wall (outer ring)
        var outerRing = new Ellipse
        {
            Width = (r + 6) * 2,
            Height = (r + 6) * 2,
            Fill = PipeWallGradient,
            Stroke = PipeWallStroke,
            StrokeThickness = 1
        };
        Canvas.SetLeft(outerRing, cx - r - 6);
        Canvas.SetTop(outerRing, cy - r - 6);
        canvas.Children.Add(outerRing);

        // Pipe interior
        var inner = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = PipeInteriorBrush
        };
        Canvas.SetLeft(inner, cx - r);
        Canvas.SetTop(inner, cy - r);
        canvas.Children.Add(inner);

        // Water level
        if (ratio > 0)
        {
            var waterRect = new System.Windows.Shapes.Rectangle
            {
                Width = r * 2,
                Height = cy + r - waterY,
                Fill = WaterGradient
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
                Fill = WaterGradient
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
                    Stroke = WaterLineBrush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection(new[] { 4.0, 2.0 })
                };
                canvas.Children.Add(surfaceLine);
            }
        }

        // DN label
        var dnLabel = new TextBlock
        {
            Text = $"DN {dMm:F0}",
            FontSize = 11,
            FontFamily = ConsolasFont,
            Foreground = LabelDimBrush
        };
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
                FontSize = 10,
                FontFamily = ConsolasFont,
                Foreground = LabelDarkBrush
            };
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
