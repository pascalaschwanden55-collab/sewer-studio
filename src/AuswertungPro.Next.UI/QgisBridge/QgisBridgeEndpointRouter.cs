using System.Text.Json;

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

            default:
                return Error(404, "Unbekannter QGIS-Bridge-Endpunkt.");
        }
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

    private static QgisBridgeResponse Json(int statusCode, object payload)
        => new(statusCode, "application/json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    private static QgisBridgeResponse GeoJson(object payload)
        => new(200, "application/geo+json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    internal static QgisBridgeResponse Error(int statusCode, string message)
        => Json(statusCode, new { ok = false, error = message });
}
