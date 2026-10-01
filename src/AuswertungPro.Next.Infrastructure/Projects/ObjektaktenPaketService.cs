using System.Text.Json;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Projects;

public sealed class ObjektaktenPaketService : IObjektaktenPaketService
{
    public void Exportiere(Project projekt, string ziel)
    {
        var paket = new ObjektaktenPaket
        {
            ProjektId = projekt.Id, Katalogstand = FieldCatalog.Objektfelder.Stand, Akten = projekt.Objektakten,
            Haltungsfelder = projekt.Data.ToDictionary(r => r.Id, r => new Dictionary<string, string>(r.Fields)),
            Schachtfelder = projekt.SchaechteData.ToDictionary(r => r.Id, r => new Dictionary<string, string>(r.Fields)),
            Haltungsmetadaten = projekt.Data.ToDictionary(r => r.Id, r => r.FieldMeta),
            Schachtmetadaten = projekt.SchaechteData.ToDictionary(r => r.Id, r => r.FieldMeta),
            Uebertragungsbericht = $"{projekt.Data.Count} Haltungen, {projekt.SchaechteData.Count} Schächte, " +
                $"{projekt.Objektakten.Count} Objektakten vollständig in dieser Zusatzdatei. " +
                "Der vollständige neue XTF-Weg überträgt belegte GeoShop-Angaben im DSS-Modell; sein Bericht nennt den tatsächlichen Lieferumfang. " +
                "Weitere Aktenwerte und Rohbeziehungen bleiben vollständig hier erhalten. " +
                "Die automatische Rückübernahme dieser Zusatzdatei in GEONIS ist nicht bestätigt."
        };
        var full = Path.GetFullPath(ziel);
        if (!string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bitte eine neue JSON-Datei wählen.");
        if (File.Exists(full) || Directory.Exists(full)) throw new IOException("Das Ausgabeziel existiert bereits. Bitte einen neuen Namen wählen.");
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { JsonSerializer.Serialize(stream, paket, JsonProjectRepository.SerializerOptions); stream.Flush(true); }
            var pruefung = Lies(temp);
            if (pruefung.ProjektId != projekt.Id || pruefung.Akten.Count != paket.Akten.Count)
                throw new InvalidDataException("Die Rückprüfung der Zusatzdatei ist fehlgeschlagen.");
            File.Move(temp, full, false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public ObjektaktenPaket Lies(string datei)
    {
        using var stream = new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 128L * 1024 * 1024) throw new InvalidDataException("Die Zusatzdatei überschreitet 128 MB.");
        var paket = JsonSerializer.Deserialize<ObjektaktenPaket>(stream, JsonProjectRepository.SerializerOptions)
            ?? throw new InvalidDataException("Die Zusatzdatei ist leer.");
        if (paket.Format != "SewerStudio.Objektakten" || paket.Version != 1 || paket.ProjektId == Guid.Empty
            || paket.Akten is null || paket.Haltungsfelder is null || paket.Schachtfelder is null
            || paket.Akten.Any(a => a is null || a.Id == Guid.Empty || a.Werte is null || a.Bezuege is null || a.Quellen is null)
            || paket.Akten.Select(a => a.Id).Distinct().Count() != paket.Akten.Count)
            throw new InvalidDataException("Ungültige Struktur oder unbekannte Version der Zusatzdatei.");
        return paket;
    }
}
