using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuswertungPro.Next.Application.Video;

/// <summary>Live-Zustand des Players samt allem, was zur Meterrechnung noetig ist.</summary>
/// <param name="Haltung">Name der laufenden Haltung — muss zu "haltung" in current.geojson passen.</param>
/// <param name="Zeit">Aktuelle Wiedergabezeit.</param>
/// <param name="Dauer">Gesamtlaenge des Videos, sofern bekannt.</param>
/// <param name="Playing">Laeuft die Wiedergabe gerade.</param>
/// <param name="HaltungslaengeM">Katasterlaenge der Haltung, sofern erfasst.</param>
/// <param name="Stuetzstellen">Codierte Beobachtungen mit Videozeit und Meter.</param>
public sealed record VideoPositionEingang(
    string Haltung,
    TimeSpan Zeit,
    TimeSpan? Dauer,
    bool Playing,
    double? HaltungslaengeM,
    IReadOnlyList<VideoMeterStuetzstelle> Stuetzstellen);

/// <summary>Antwort des Endpunkts /qgis/video_position.json.</summary>
public sealed record VideoPositionAntwort(
    string Haltung,
    double Meter,
    string? Zeit,
    double? Laenge,
    bool Playing,
    VideoMeterQuelle Quelle);

/// <summary>
/// Fuehrt Player-Zustand und Stuetzstellen zur Antwort zusammen.
///
/// Liefert <c>null</c>, sobald etwas Wesentliches fehlt — Haltungsname oder ein
/// belastbarer Meterwert. Der Endpunkt antwortet dann mit 404 und das QGIS-Plugin
/// bleibt still. Ein geratener Marker waere schlimmer als gar keiner.
///
/// Reine Rechnung ohne Oberflaeche, Datei- oder Netzzugriff.
/// </summary>
public static class VideoPositionRechnung
{
    public static VideoPositionAntwort? Rechne(VideoPositionEingang? eingang)
    {
        if (eingang is null || string.IsNullOrWhiteSpace(eingang.Haltung))
            return null;

        var spur = VideoMeterSpur.Baue(
            eingang.Stuetzstellen,
            eingang.HaltungslaengeM,
            eingang.Dauer);

        if (spur.MeterBei(eingang.Zeit) is not { } meter)
            return null;

        return new VideoPositionAntwort(
            eingang.Haltung.Trim(),
            Math.Round(meter, 2),
            Zeittext(eingang.Zeit),
            eingang.HaltungslaengeM,
            eingang.Playing,
            spur.Quelle);
    }

    /// <summary>Nur fuer die Textanzeige im Plugin — nie Grundlage einer Rechnung.</summary>
    private static string? Zeittext(TimeSpan zeit)
        => zeit < TimeSpan.Zero
            ? null
            : zeit.ToString(@"hh\:mm\:ss\.f", CultureInfo.InvariantCulture);
}
