using System;
using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Liefert ein Quellen-Icon fuer einen <see cref="ProtocolEntrySource"/>:
/// AI = Roboter, Heuristik = Zahnrad, Manual = Person, Imported = Dokument.
/// Macht in der UI sichtbar, ob ein Eintrag aus echter Frame-Analyse oder
/// aus Auto-Heuristik (z.B. EnsureRohranfangExists) stammt.
/// </summary>
public sealed class ProtocolEntrySourceToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            ProtocolEntrySource.Ai        => "\U0001F916", // 🤖
            ProtocolEntrySource.Heuristic => "⚙",     // ⚙
            ProtocolEntrySource.Manual    => "\U0001F464", // 👤
            ProtocolEntrySource.Imported  => "\U0001F4C4", // 📄
            _ => ""
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Liefert eine ausgeschriebene Quellen-Bezeichnung fuer den ToolTip.
/// </summary>
public sealed class ProtocolEntrySourceToTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            ProtocolEntrySource.Ai        => "KI-Detektion (YOLO/Qwen)",
            ProtocolEntrySource.Heuristic => "Auto-Heuristik (kein Frame analysiert)",
            ProtocolEntrySource.Manual    => "Manuell vom User eingetragen",
            ProtocolEntrySource.Imported  => "Aus PDF/Protokoll importiert",
            _ => "Unbekannte Quelle"
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
