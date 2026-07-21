using System.Globalization;
using System.IO;
using System.Windows.Data;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Converter: non-null → true, null → false.</summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Wandelt einen VSA-Code in "CODE — Klartext" um (Klartext aus dem Katalog via VsaCodeResolver).
/// Ist der Wert kein bekannter Code (z.B. "nichts erkannt"), bleibt er unveraendert.
/// </summary>
public sealed class VsaCodeToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string code || string.IsNullOrWhiteSpace(code))
            return value;

        var label = Infrastructure.Ai.VsaCodeResolver.LookupLabel(code);
        return string.IsNullOrWhiteSpace(label) ? code : $"{code} — {label}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Laedt ein Bild aus dem Dateipfad in den Speicher, ohne die Datei zu sperren.
/// Verhindert File-Locking und ermoeglicht Echtzeit-Updates waehrend Self-Training.
/// </summary>
public sealed class FileToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        if (!File.Exists(path))
        {
            BestEffort.ReportWarning($"[FileToImage] Datei nicht gefunden: {path}");
            return null;
        }

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            bmp.DecodePixelWidth = ResolveDecodePixelWidth(parameter);
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[FileToImage] Fehler beim Laden: {path} → {ex.Message}");
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static int ResolveDecodePixelWidth(object? parameter)
    {
        if (parameter is int width && width > 0)
            return width;

        if (parameter is string text
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return 480;
    }
}
