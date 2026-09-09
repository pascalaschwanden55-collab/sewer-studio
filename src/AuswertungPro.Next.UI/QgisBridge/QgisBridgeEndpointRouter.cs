using System.Text.Json;
using AuswertungPro.Next.Application.Video;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Ordnet die lesenden Bridge-Pfade ihren JSON-/GeoJSON-Antworten zu.
/// UI-Aufnahme und HTTP-Hosting bleiben dadurch getrennt testbar.
/// </summary>
internal sealed class QgisBridgeEndpointRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly QgisBridgeSnapshotBuilder _builder;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, (QgisPayloadFingerprint Fingerprint, QgisBridgeResponse Response)> _payloadCache = new();

    public QgisBridgeEndpointRouter(QgisBridgeSnapshotBuilder builder)
    {
        _builder = builder;
    }

    public QgisBridgeResponse Route(string path, QgisProjectSnapshot snapshot)
    {
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        switch (path)
        {
            case "/" or "/qgis" or "/qgis/" or "/qgis/status.json":
                return Json(200, _builder.BuildStatus(snapshot));

            case "/qgis/current.geojson":
                return GetOrBuildGeoJson(
                    "current",
                    snapshot.CurrentFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildCurrentGeoJson(snapshot));

            case "/qgis/damages.geojson":
                return GetOrBuildGeoJson(
                    "damages",
                    snapshot.DamagesFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildDamagesGeoJson(snapshot));

            case "/qgis/network.geojson":
                return GetOrBuildGeoJson(
                    "network",
                    snapshot.NetworkFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildNetworkGeoJson(snapshot));

            case "/qgis/sanierungstyp.geojson":
                return GetOrBuildGeoJson(
                    "sanierungstyp",
                    snapshot.SanierungstypFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildSanierungstypGeoJson(snapshot));

            case "/qgis/schaechte.geojson":
                return GetOrBuildGeoJson(
                    "schaechte",
                    snapshot.SchaechteFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildSchaechteGeoJson(snapshot));

            case "/qgis/current_schacht.geojson":
                return GetOrBuildGeoJson(
                    "current_schacht",
                    snapshot.CurrentSchachtFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildCurrentSchachtGeoJson(snapshot));

            case "/qgis/schacht_sanierungstyp.geojson":
                return GetOrBuildGeoJson(
                    "schacht_sanierungstyp",
                    snapshot.SchachtSanierungstypFingerprint(_builder.GetNetworkStampTicks()),
                    () => _builder.BuildSchachtSanierungstypGeoJson(snapshot));

            case "/qgis/video_position.json":
                return VideoPosition();

            default:
                return Error(404, "Unbekannter QGIS-Bridge-Endpunkt.");
        }
    }

    /// <summary>
    /// Live-Position der Videowiedergabe. Bewusst OHNE Zwischenspeicher: Der Wert
    /// aendert sich mehrmals je Sekunde, und das QGIS-Plugin fragt in eigenem Takt.
    /// Laeuft kein Video oder ist der Meterstand nicht bestimmbar, gibt es 404 —
    /// das Plugin bleibt dann still, statt einen geratenen Marker zu zeigen.
    /// </summary>
    private static QgisBridgeResponse VideoPosition()
    {
        var position = QgisBridgeVideoPosition.Lies();
        if (position is null)
            return Error(404, "Zurzeit laeuft kein Video mit bestimmbarem Meterstand.");

        return Json(200, new
        {
            haltung = position.Haltung,
            meter = position.Meter,
            zeit = position.Zeit,
            laenge = position.Laenge,
            playing = position.Playing,
            // Herkunft des Meterwerts: "stuetzstellen" ist die codierte Zuordnung,
            // "mittlereGeschwindigkeit" nur eine Grobortung. Das gehoert in die
            // Antwort, damit eine Schaetzung nicht wie eine Messung aussieht.
            meterQuelle = position.Quelle.ToString()
        });
    }

    private QgisBridgeResponse GetOrBuildGeoJson(
        string cacheKey,
        QgisPayloadFingerprint fingerprint,
        Func<object> build)
    {
        lock (_cacheGate)
        {
            if (_payloadCache.TryGetValue(cacheKey, out var hit) && hit.Fingerprint.Equals(fingerprint))
                return hit.Response;
        }

        var response = GeoJson(build());
        lock (_cacheGate)
            _payloadCache[cacheKey] = (fingerprint, response);
        return response;
    }

    /// <summary>
    /// Der einzige schreibende Weg der Bruecke. Bewusst getrennt von <see cref="Route"/>:
    /// Ein lesender Pfad soll nie versehentlich etwas veraendern koennen, und ein
    /// unbekannter Pfad bleibt hier ein 404 statt einer stillen Wirkung.
    /// </summary>
    public QgisBridgeResponse RoutePost(string path, string? body)
    {
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        return path switch
        {
            "/qgis/seek" => Seek(body),
            _ => Error(404, "Unbekannter schreibender QGIS-Bridge-Endpunkt.")
        };
    }

    /// <summary>
    /// Klick in der QGIS-Karte: im laufenden Video an diesen Meter springen.
    /// Jeder Grund, der keinen Sprung ergibt, wird benannt — das QGIS-Plugin zeigt
    /// ihn im Status an, statt so zu tun, als sei etwas passiert.
    /// </summary>
    private static QgisBridgeResponse Seek(string? body)
    {
        if (!QgisSeekAnfrage.TryLies(body, out var auftrag) || auftrag is null)
            return Error(400, "Erwartet wird {\"haltung\": \"...\", \"meter\": 12.3}.");

        var grund = QgisBridgeVideoSprung.Springe(auftrag);
        return grund switch
        {
            VideoSprungGrund.Bereit => Json(200, new { ok = true }),
            VideoSprungGrund.KeinVideo => Error(404, "Zurzeit laeuft kein Video."),
            VideoSprungGrund.KeinAuftrag => Error(400, "Der Auftrag nennt keine Haltung."),
            VideoSprungGrund.UngueltigerMeter => Error(400, "Der Meterwert ist keine brauchbare Zahl."),
            VideoSprungGrund.FremdeHaltung => Error(409, "Im Video laeuft eine andere Haltung."),
            VideoSprungGrund.NichtBestimmbar => Error(409, "Zu dieser Stelle ist keine Videozeit bestimmbar."),
            _ => Error(409, "Der Sprung wurde nicht ausgefuehrt.")
        };
    }

    private static QgisBridgeResponse Json(int statusCode, object payload)
        => new(statusCode, "application/json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    private static QgisBridgeResponse GeoJson(object payload)
        => new(200, "application/geo+json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    internal static QgisBridgeResponse Error(int statusCode, string message)
        => Json(statusCode, new { ok = false, error = message });
}
