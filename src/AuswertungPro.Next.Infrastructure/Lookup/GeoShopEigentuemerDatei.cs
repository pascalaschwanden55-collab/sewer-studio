using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Lookup;

public static class GeoShopEigentuemerDatei
{
    public static IReadOnlyDictionary<string, string> Lies(string datei)
    {
        using var stream = new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("Die Eigentümerdatei überschreitet 4 MB.");
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Erwartet wird eine Zuordnung von Originalkennung zu Eigentümername.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in doc.RootElement.EnumerateObject())
            if (!SiaObjektkennung.IstGueltig(p.Name) || p.Value.ValueKind != JsonValueKind.String
                || !result.TryAdd(p.Name, p.Value.GetString()!))
                throw new InvalidDataException("Ungültige oder doppelte Eigentümerkennung: " + p.Name);
        return result;
    }
}
