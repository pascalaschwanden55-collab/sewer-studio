using System;
using System.Threading;
using Mapsui.Layers;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die produktive Ordnerpruefung
/// und Layer-Erzeugung liegen im injizierbaren <see cref="IKarteBasemapLayerFactory"/>.
/// </summary>
public static class KarteBasemapLayerFactory
{
    public const string SatellitLayerName = "Satellit";
    public const string AvLayerName = "AV-Karte";
    public const string OsmLayerName = "OpenStreetMap";

    public const string SatellitSubfolder = "satellit";
    public const string AvSubfolder = "av";

    private static IKarteBasemapLayerFactory _current = new KarteBasemapLayerService();

    internal static IKarteBasemapLayerFactory CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(IKarteBasemapLayerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Volatile.Write(ref _current, factory);
    }

    public static ILayer? CreateOfflineSatellit(string? basePath)
        => CompatibilityService.CreateOfflineSatellit(basePath);

    public static ILayer? CreateOfflineAv(string? basePath)
        => CompatibilityService.CreateOfflineAv(basePath);

    public static ILayer CreateOsmOnline()
        => CompatibilityService.CreateOsmOnline();
}
