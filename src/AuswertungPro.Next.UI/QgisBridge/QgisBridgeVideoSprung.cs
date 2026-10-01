using AuswertungPro.Next.Application.Video;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Rueckweg der Bruecke: ein Klick in der QGIS-Karte laesst das Video springen.
///
/// Aufbau wie bei <see cref="QgisBridgeVideoPosition"/> — das Ziel wird beim Start
/// einmal gesetzt, dadurch bleibt der Router ohne Player testbar. Ist kein Ziel
/// gesetzt, laeuft schlicht kein Video: der Endpunkt meldet das, und in QGIS
/// aendert sich nichts.
///
/// Bewusst der einzige schreibende Weg der Bruecke, und er kann nur eines —
/// im offenen Video an eine andere Stelle springen. Er oeffnet kein Video, wechselt
/// keine Haltung und veraendert keine Daten.
/// </summary>
internal static class QgisBridgeVideoSprung
{
    private static readonly object Gate = new();
    private static Func<VideoSprungAuftrag, VideoSprungGrund>? _ziel;

    public static void SetzeZiel(Func<VideoSprungAuftrag, VideoSprungGrund>? ziel)
    {
        lock (Gate)
            _ziel = ziel;
    }

    /// <summary>Fuehrt den Sprung aus und meldet, was daraus wurde. Wirft nie.</summary>
    public static VideoSprungGrund Springe(VideoSprungAuftrag auftrag)
    {
        Func<VideoSprungAuftrag, VideoSprungGrund>? ziel;
        lock (Gate)
            ziel = _ziel;

        if (ziel is null)
            return VideoSprungGrund.KeinVideo;

        try
        {
            return ziel(auftrag);
        }
        catch
        {
            // Eine Stoerung beim Springen darf die uebrige Bruecke nicht mitreissen.
            return VideoSprungGrund.SprungFehlgeschlagen;
        }
    }
}
