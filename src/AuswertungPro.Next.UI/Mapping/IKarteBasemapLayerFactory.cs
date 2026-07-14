using Mapsui.Layers;

namespace AuswertungPro.Next.UI.Mapping;

public interface IKarteBasemapLayerFactory
{
    ILayer? CreateOfflineSatellit(string? basePath);

    ILayer? CreateOfflineAv(string? basePath);

    ILayer CreateOsmOnline();
}
