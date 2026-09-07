using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Beschriftung der Zustandsklassen-Marke — "Z0" bis "Z4".
///
/// Die Klasse wird ueber <see cref="ZustandsklasseColorPalette.NormalizeClass"/> gelesen,
/// also genau so wie die Hintergrundfarbe der Marke. Text und Farbe koennen dadurch nie
/// auseinanderlaufen. Ohne gueltige Klasse bleibt es beim Gedankenstrich: Der Chip erfindet
/// nie eine Zahl, und ein Strich ist der getrennte Zustand "nicht berechnet" — niemals Z4.
/// </summary>
public sealed class ZustandsklasseChipTextConverter : IValueConverter
{
    public const string OhneKlasse = "–";
    public static readonly ZustandsklasseChipTextConverter Instance = new();

    public static string Text(object? value)
    {
        var klasse = ZustandsklasseColorPalette.NormalizeClass(value);
        return klasse.Length == 0 ? OhneKlasse : $"Z{klasse}";
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Text(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Hintergrund der Marke: die Klassenfarbe, sonst nichts (die leere Marke ist umrandet).</summary>
public sealed class ZustandsklasseChipHintergrundConverter : IValueConverter
{
    public static readonly ZustandsklasseChipHintergrundConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) ?? (object)Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Zeigt entweder die gefuellte Marke (<see cref="Gefuellt"/>) oder die gestrichelte leere
/// Marke (<see cref="Leer"/>). Beide liegen in derselben Zelle uebereinander.
/// </summary>
public sealed class ZustandsklasseChipSichtbarkeitConverter : IValueConverter
{
    public const string Gefuellt = "Gefuellt";
    public const string Leer = "Leer";
    public static readonly ZustandsklasseChipSichtbarkeitConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hatKlasse = ZustandsklasseColorPalette.NormalizeClass(value).Length > 0;
        var sichtbar = (parameter as string) == Leer ? !hatKlasse : hatKlasse;
        return sichtbar ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
