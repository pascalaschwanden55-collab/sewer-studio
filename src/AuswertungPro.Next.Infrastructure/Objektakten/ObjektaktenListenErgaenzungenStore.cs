using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.Objektakten;

namespace AuswertungPro.Next.Infrastructure.Objektakten;

/// <summary>Eigene Listeneintraege und Korrekturen, programmweit in AppData - nach dem Muster der
/// zusaetzlichen Sicherungsordner: atomar geschrieben, von der Vollsicherung erfasst.
/// Der eingebaute Katalog wird nie angefasst; diese Datei liegt beim Aufbau der Liste darueber.</summary>
public sealed class ObjektaktenListenErgaenzungenStore(string settingsFolder) : IObjektaktenListenErgaenzungen
{
    public const string Dateiname = "objektakten-listen-ergaenzungen.json";
    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    private readonly string _datei = Path.Combine(settingsFolder, Dateiname);

    public IReadOnlyList<ListenErgaenzung> Lade()
    {
        if (!File.Exists(_datei)) return [];
        Dokument? dokument;
        try
        {
            dokument = JsonSerializer.Deserialize<Dokument>(File.ReadAllText(_datei), Optionen);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Die Listenergänzungen in '{_datei}' sind nicht lesbar.", ex);
        }
        if (dokument is null || dokument.Version != 1 || dokument.Eintraege is null
            || dokument.Eintraege.Any(e => e is null))
            throw new InvalidDataException($"Die Listenergänzungen in '{_datei}' sind ungültig.");
        foreach (var eintrag in dokument.Eintraege) eintrag!.Pruefe();
        return dokument.Eintraege!;
    }

    public void Speichere(IEnumerable<ListenErgaenzung> ergaenzungen)
    {
        var liste = ergaenzungen.ToList();
        foreach (var eintrag in liste) eintrag.Pruefe();
        var text = JsonSerializer.Serialize(new Dokument { Version = 1, Eintraege = liste }, Optionen);
        AtomicTextFileWriter.WriteAllText(_datei, text, durable: true);
    }

    private sealed class Dokument
    {
        public int Version { get; set; }
        public List<ListenErgaenzung?>? Eintraege { get; set; }
    }
}
