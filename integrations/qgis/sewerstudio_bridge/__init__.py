from .sewerstudio_bridge import SewerStudioBridgePlugin


def classFactory(iface):
    return SewerStudioBridgePlugin(iface)
