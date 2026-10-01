using AuswertungPro.Next.Application.Video;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Zugang der Bruecke zur laufenden Videowiedergabe.
///
/// Die Quelle wird beim Start einmal gesetzt (auf den Player). Dadurch bleibt der
/// Router ohne Player testbar, und die Bruecke haengt nicht fest am Fenster.
/// Ist keine Quelle gesetzt oder laeuft kein Video, gibt es keine Position — der
/// Endpunkt antwortet dann mit 404 und das QGIS-Plugin bleibt still.
/// </summary>
internal static class QgisBridgeVideoPosition
{
    private static readonly object Gate = new();
    private static Func<VideoPositionEingang?>? _quelle;

    public static void SetzeQuelle(Func<VideoPositionEingang?>? quelle)
    {
        lock (Gate)
            _quelle = quelle;
    }

    /// <summary>Aktuelle Position oder <c>null</c>. Wirft nie.</summary>
    public static VideoPositionAntwort? Lies()
    {
        Func<VideoPositionEingang?>? quelle;
        lock (Gate)
            quelle = _quelle;

        if (quelle is null)
            return null;

        try
        {
            return VideoPositionRechnung.Rechne(quelle());
        }
        catch
        {
            // Eine Stoerung beim Lesen darf die uebrige Bruecke nicht mitreissen.
            return null;
        }
    }
}
