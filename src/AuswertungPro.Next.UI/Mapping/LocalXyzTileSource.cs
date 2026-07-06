using System.IO;
using System.Threading.Tasks;
using BruTile;
using BruTile.Predefined;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Liest lokale XYZ-Kacheln (von QGIS via qgis_process erzeugt) aus einem Ordner
/// in der Struktur {z}/{x}/{y}.png. Kein Server, kein Zusatzpaket – nutzt BruTile,
/// das ueber Mapsui ohnehin vorhanden ist. Liefert null fuer fehlende Kacheln
/// (ausserhalb des exportierten Bereichs bleibt der Hintergrund leer).
/// </summary>
public sealed class LocalXyzTileSource : ILocalTileSource
{
    private readonly string _root;
    private readonly string _extension;

    public ITileSchema Schema { get; }
    public string Name { get; }
    public Attribution Attribution { get; }

    public LocalXyzTileSource(string root, string name = "QGIS", string fileExtension = ".png", string attribution = "QGIS")
    {
        _root = root;
        _extension = fileExtension.StartsWith('.') ? fileExtension : "." + fileExtension;
        Name = name;
        Attribution = new Attribution(attribution);
        // XYZ-Konvention (Google/OSM, Y von oben) – genau das erzeugt qgis_process bzw. swisstopo-WMTS/3857.
        Schema = new GlobalSphericalMercator(YAxis.OSM);
    }

    public Task<byte[]?> GetTileAsync(TileInfo tileInfo)
    {
        var index = tileInfo.Index;
        var path = Path.Combine(_root, index.Level.ToString(), index.Col.ToString(), index.Row + _extension);
        return Task.FromResult(File.Exists(path) ? File.ReadAllBytes(path) : null);
    }
}
