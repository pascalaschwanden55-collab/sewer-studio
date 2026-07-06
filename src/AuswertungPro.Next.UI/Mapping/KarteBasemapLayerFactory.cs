using System.IO;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Baut die Hintergrundkarten-Layer der App-Karte: Satellit (SWISSIMAGE) und AV-Karte farbig
/// (Amtliche Vermessung/Grundbuch) als lokale Offline-Kacheln im Programmordner sowie
/// OpenStreetMap online. Bewusst eigene Einheit, damit BuildMapAsync im KarteViewModel schlank
/// bleibt (keine God-Class). Die Offline-Ordner liegen als Unterordner unter dem Basispfad.
/// </summary>
public static class KarteBasemapLayerFactory
{
    public const string SatellitLayerName = "Satellit";
    public const string AvLayerName = "AV-Karte";
    public const string OsmLayerName = "OpenStreetMap";

    // Unterordner unter OfflineBasemapPath (so wie der Downloader sie ablegt).
    public const string SatellitSubfolder = "satellit";
    public const string AvSubfolder = "av";

    // OSM-Standardkacheln (EPSG:3857, XYZ). Nutzungsbedingungen: gelegentliche Nutzung ok,
    // User-Agent Pflicht (in OnlineXyzTileSource gesetzt).
    private const string OsmUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>Satellit-Kacheln (SWISSIMAGE, JPEG) aus dem Unterordner. null, wenn Ordner fehlt/leer.</summary>
    public static ILayer? CreateOfflineSatellit(string? basePath)
        => CreateOffline(basePath, SatellitSubfolder, SatellitLayerName, ".jpeg", "© swisstopo (SWISSIMAGE, offline)");

    /// <summary>AV-Karte farbig (Grundbuch, PNG) aus dem Unterordner. null, wenn Ordner fehlt/leer.</summary>
    public static ILayer? CreateOfflineAv(string? basePath)
        => CreateOffline(basePath, AvSubfolder, AvLayerName, ".png", "© Amtliche Vermessung / swisstopo (offline)");

    /// <summary>OpenStreetMap online. Braucht Internet.</summary>
    public static ILayer CreateOsmOnline()
    {
        var source = new OnlineXyzTileSource(OsmUrl, OsmLayerName, "© OpenStreetMap-Mitwirkende");
        return new TileLayer(source) { Name = OsmLayerName };
    }

    private static ILayer? CreateOffline(string? basePath, string subfolder, string layerName, string extension, string attribution)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            return null;

        var root = Path.Combine(basePath, subfolder);
        if (!Directory.Exists(root))
            return null;

        var source = new LocalXyzTileSource(root, layerName, extension, attribution);
        return new TileLayer(source) { Name = layerName };
    }
}
