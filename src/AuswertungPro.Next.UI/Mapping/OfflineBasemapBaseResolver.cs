using System;
using System.Threading;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Findet den Basisordner der Offline-Hintergrundkarten (mit den Unterordnern "satellit"/"av").
/// Toleriert einen veralteten gespeicherten Pfad: fruehere Versionen speicherten
/// "...\basemap_tiles\uri" in der settings.json, die Kacheln liegen aber unter
/// "...\basemap_tiles\{satellit,av}". Der Resolver prueft daher den Pfad selbst UND seinen
/// Elternordner. So laedt die Satellit-/AV-Karte auch ohne die settings.json von Hand zu
/// aendern. Reine Ordner-Pruefung -> gut testbar, keine God-Class.
/// </summary>
public static class OfflineBasemapBaseResolver
{
    private static IOfflineBasemapPathResolver _current = new OfflineBasemapDirectoryResolver();

    internal static IOfflineBasemapPathResolver CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(IOfflineBasemapPathResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _current, resolver);
    }

    public static string? Resolve(string? configuredPath)
    {
        return CompatibilityService.Resolve(configuredPath);
    }
}
