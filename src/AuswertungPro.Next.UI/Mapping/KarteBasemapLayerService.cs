using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using System.IO;

namespace AuswertungPro.Next.UI.Mapping;

public sealed class KarteBasemapLayerService : IKarteBasemapLayerFactory
{
    private const string OsmUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    public ILayer? CreateOfflineSatellit(string? basePath)
        => CreateOffline(
            basePath,
            KarteBasemapLayerFactory.SatellitSubfolder,
            KarteBasemapLayerFactory.SatellitLayerName,
            ".jpeg",
            "Â© swisstopo (SWISSIMAGE, offline)");

    public ILayer? CreateOfflineAv(string? basePath)
        => CreateOffline(
            basePath,
            KarteBasemapLayerFactory.AvSubfolder,
            KarteBasemapLayerFactory.AvLayerName,
            ".png",
            "Â© Amtliche Vermessung / swisstopo (offline)");

    public ILayer CreateOsmOnline()
    {
        var source = new OnlineXyzTileSource(
            OsmUrl,
            KarteBasemapLayerFactory.OsmLayerName,
            "Â© OpenStreetMap-Mitwirkende");
        return new TileLayer(source) { Name = KarteBasemapLayerFactory.OsmLayerName };
    }

    private static ILayer? CreateOffline(
        string? basePath,
        string subfolder,
        string layerName,
        string extension,
        string attribution)
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
