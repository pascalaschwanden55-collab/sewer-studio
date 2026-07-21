using System;
using System.Net.Http;
using System.Threading.Tasks;
using BruTile;
using BruTile.Predefined;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Online-Kachelquelle (EPSG:3857, XYZ/OSM-Konvention) — holt Kacheln per HTTP; liefert null
/// bei Fehler/kein Netz (dann bleibt der Ausschnitt leer, ohne Absturz). Dieselbe
/// ILocalTileSource-Form wie <see cref="LocalXyzTileSource"/>, damit Offline und Online
/// austauschbar sind. Setzt einen User-Agent (von OSM-Kachelservern verlangt).
/// </summary>
public sealed class OnlineXyzTileSource : ILocalTileSource
{
    private static readonly HttpClient Http = CreateClient();
    private readonly string _urlTemplate;

    public ITileSchema Schema { get; } = new GlobalSphericalMercator(YAxis.OSM);
    public string Name { get; }
    public Attribution Attribution { get; }

    /// <param name="urlTemplate">URL mit Platzhaltern {z}/{x}/{y}.</param>
    public OnlineXyzTileSource(string urlTemplate, string name, string attribution)
    {
        _urlTemplate = urlTemplate;
        Name = name;
        Attribution = new Attribution(attribution);
    }

    public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
    {
        var index = tileInfo.Index;
        var url = _urlTemplate
            .Replace("{z}", index.Level.ToString())
            .Replace("{x}", index.Col.ToString())
            .Replace("{y}", index.Row.ToString());
        try
        {
            return await Http.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        // Kacheln sind klein; bei totem Netz (Blackhole/Captive Portal) soll die Karte schnell
        // auf "leer" fallen statt bis zum 100-s-Default zu haengen und Anfragen zu stauen.
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // OSM-Kachelserver verlangen einen aussagekraeftigen, ehrlichen User-Agent, der die
        // Anwendung identifiziert (sonst "403 Access blocked"). OSM ist hier nur die Online-
        // Ausweichkarte — Standard ist die Offline-Satellitenkarte, damit OSM nicht bei jedem
        // Start mit einer ganzen Bildschirmfuellung Kacheln beansprucht wird.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SewerStudio/1.0 Kanalinspektion (Windows-Desktop; interaktive Nutzung)");
        return http;
    }
}
