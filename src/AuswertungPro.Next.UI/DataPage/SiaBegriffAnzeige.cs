using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Lesbare Beschriftung; der ausgewaehlte und gespeicherte Normwert bleibt exakt erhalten.</summary>
public sealed class SiaBegriffAnzeige : IValueConverter
{
    public static string Klartext(string wert)
    {
        if (!AbwasserbauwerkVokabular.Spezialfunktionen.Contains(wert)
            && !AbwasserbauwerkVokabular.Versickerungsarten.Contains(wert)) return wert;
        return wert switch
        {
            "abflussloseGrube" => "Abflusslose Grube",
            "seitlicherZugang" => "Seitlicher Zugang",
            "MuldenRigolenversickerung" => "Mulden-Rigolenversickerung",
            "Kontroll_Einsteigschacht" => "Kontroll-/Einsteigschacht",
            "Be_Entlueftung" => "Be-/Entlüftung",
            _ => wert.Replace("_", " ", StringComparison.Ordinal)
                .Replace("ue", "ü", StringComparison.Ordinal).Replace("oe", "ö", StringComparison.Ordinal)
                .Replace("ae", "ä", StringComparison.Ordinal).Replace("Oe", "Ö", StringComparison.Ordinal)
        };
    }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string text ? Klartext(text) : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
