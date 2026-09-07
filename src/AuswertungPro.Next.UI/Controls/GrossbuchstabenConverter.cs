using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Schreibt einen String-Tabellenkopf in Grossbuchstaben.
/// Kapitaelchen (<c>Typography.Capitals</c>) greifen mit der Programmschrift nicht (Prototyp
/// nutzt echte Grossbuchstaben mit Letter-Spacing). Andere Inhalte (z. B. eigene Kopf-Elemente)
/// bleiben unveraendert, damit ein nicht-textueller Spaltenkopf nicht kaputtgeht.
/// </summary>
public sealed class GrossbuchstabenConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string text ? text.ToUpperInvariant() : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
