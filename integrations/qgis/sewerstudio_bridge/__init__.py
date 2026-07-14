def classFactory(iface):
    # Erst beim QGIS-Einstieg importieren. Dadurch bleibt der reine HTTP-Vertrag
    # ohne installierte QGIS-Bibliotheken testbar.
    from .sewerstudio_bridge import SewerStudioBridgePlugin

    return SewerStudioBridgePlugin(iface)
