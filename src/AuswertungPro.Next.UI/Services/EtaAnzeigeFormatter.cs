using System;
using System.Globalization;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Kompakte ETA-Zeile fuer Statusleisten: "12.4 Frames/s · Rest ~ 04:12".</summary>
public static class EtaAnzeigeFormatter
{
    private static readonly CultureInfo Kultur = CultureInfo.GetCultureInfo("de-CH");

    public static string Format(EtaErgebnis? eta)
    {
        if (eta?.RateProSekunde is not { } rate || rate <= 0)
            return string.Empty;

        var rateText = $"{rate.ToString("0.0", Kultur)} Frames/s";
        if (eta.Restzeit is not { } rest)
            return rateText;

        var restText = rest.TotalHours >= 1
            ? $"{(int)rest.TotalHours}:{rest.Minutes:00}:{rest.Seconds:00}"
            : $"{rest.Minutes:00}:{rest.Seconds:00}";
        return $"{rateText} · Rest ~ {restText}";
    }
}
