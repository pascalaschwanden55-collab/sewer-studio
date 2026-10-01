using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Infrastructure.Lookup;

public static class GeoShopEigentuemerDatei
{
    /// <summary>Die benannte Begleitdatei derselben Lieferung verwenden; fremde JSON-Dateien nicht durchsuchen.</summary>
    public static GeoShopBestand ErgaenzeBegleitdatei(GeoShopBestand bestand)
    {
        var datei = Path.Combine(Path.GetDirectoryName(bestand.Quelle)!, "eigentuemer_zuordnung.json");
        if (!File.Exists(datei)) return bestand;
        try
        {
            return AuswertungPro.Next.Application.UseCases.Objektakten.GeoShopEigentuemerErgaenzung
                .Ergaenze(bestand, Lies(datei), datei);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return bestand with { Bauteile = bestand.Bauteile.Select(b => b with
            { Hinweis = (b.Hinweis + $" Eigentümerdatei konnte nicht gelesen werden ({datei}): {ex.Message}").Trim() }).ToArray() };
        }
    }

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
