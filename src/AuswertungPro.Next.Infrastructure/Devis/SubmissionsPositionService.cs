using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.Infrastructure.Devis;

/// <summary>
/// Laedt den Submissions-Positionskatalog (Knowledge/sanierung/submission_positionen.json)
/// und liefert standardisierte Schweizer Submissions-Positionen pro Sanierungs-Block.
///
/// Quelle: Submission Buerglen 2026 (Abwasser Uri) + NPK/SIA-Konvention.
/// Verwendung: DevisGenerator nutzt diesen Katalog um Submissions-konforme Devise zu erstellen.
/// </summary>
public sealed class SubmissionsPositionService
{
    private readonly string _catalogPath;
    private SubmissionsCatalog? _catalog;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public SubmissionsPositionService(string catalogPath)
    {
        _catalogPath = catalogPath;
    }

    public SubmissionsCatalog LoadCatalog()
    {
        if (_catalog is not null)
            return _catalog;

        if (!File.Exists(_catalogPath))
        {
            _catalog = new SubmissionsCatalog();
            return _catalog;
        }

        var json = File.ReadAllText(_catalogPath);
        _catalog = JsonSerializer.Deserialize<SubmissionsCatalog>(json, JsonOpts) ?? new SubmissionsCatalog();
        return _catalog;
    }

    /// <summary>Setzt den Cache zurueck. Naechster LoadCatalog liest die JSON neu.</summary>
    public void Invalidate() => _catalog = null;

    /// <summary>Pfad zur aktuellen JSON-Datei (fuer Anzeige/Diagnose).</summary>
    public string FilePath => _catalogPath;

    /// <summary>Gibt den Block fuer einen Submissions-Pos-Code zurueck (z.B. "2.1.1" -> Block "200").</summary>
    public SubmissionsBlock? FindBlockForPos(string pos)
    {
        var catalog = LoadCatalog();
        foreach (var block in catalog.Blocks)
        {
            if (block.Positionen.Any(p => string.Equals(p.Pos, pos, StringComparison.OrdinalIgnoreCase)))
                return block;
        }
        return null;
    }

    /// <summary>Findet die Submissions-Position fuer einen Pos-Code.</summary>
    public SubmissionsPosition? FindPosition(string pos)
    {
        var catalog = LoadCatalog();
        return catalog.Blocks
            .SelectMany(b => b.Positionen)
            .FirstOrDefault(p => string.Equals(p.Pos, pos, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Empfiehlt Submissions-Bloecke fuer einen VSA-Code (z.B. "BAB.B" -> ["500","600"]).</summary>
    public IReadOnlyList<string> RecommendBlocksForVsaCode(string vsaCode)
    {
        var catalog = LoadCatalog();
        if (catalog.VsaZuBlock is null)
            return Array.Empty<string>();

        // Versuche zuerst exakten Match (z.B. "BAB.B"), dann nur Hauptcode (z.B. "BAB").
        var normalized = vsaCode.ToUpperInvariant().Trim();
        if (catalog.VsaZuBlock.TryGetValue(normalized, out var blocks))
            return blocks;

        if (normalized.Length >= 3)
        {
            var main = normalized.Substring(0, 3);
            if (catalog.VsaZuBlock.TryGetValue(main, out var mainBlocks))
                return mainBlocks;
        }

        return Array.Empty<string>();
    }

    /// <summary>Liefert das Quellen-Label fuer Devis-Footer.</summary>
    public string GetMarketSourceLabel()
    {
        var catalog = LoadCatalog();
        return catalog.Meta?.Quelle ?? "Submission Bürglen 2026";
    }
}

// ── Datenmodell ────────────────────────────────────────────────────────

public sealed class SubmissionsCatalog
{
    [JsonPropertyName("_meta")]
    public SubmissionsMeta? Meta { get; set; }

    [JsonPropertyName("blocks")]
    public List<SubmissionsBlock> Blocks { get; set; } = new();

    [JsonPropertyName("vsa_zu_block_mapping")]
    [JsonConverter(typeof(VsaMappingConverter))]
    public Dictionary<string, IReadOnlyList<string>>? VsaZuBlock { get; set; }
}

public sealed class SubmissionsMeta
{
    [JsonPropertyName("titel")]
    public string Titel { get; set; } = "";

    [JsonPropertyName("quelle")]
    public string Quelle { get; set; } = "";

    [JsonPropertyName("stand")]
    public string Stand { get; set; } = "";
}

public sealed class SubmissionsBlock
{
    [JsonPropertyName("block")]
    public string Block { get; set; } = "";

    [JsonPropertyName("titel")]
    public string Titel { get; set; } = "";

    [JsonPropertyName("beschreibung")]
    public string? Beschreibung { get; set; }

    [JsonPropertyName("vsa_codes_indikatoren")]
    public List<string>? VsaCodesIndikatoren { get; set; }

    [JsonPropertyName("positionen")]
    public List<SubmissionsPosition> Positionen { get; set; } = new();
}

public sealed class SubmissionsPosition
{
    [JsonPropertyName("pos")]
    public string Pos { get; set; } = "";

    [JsonPropertyName("bezeichnung")]
    public string Bezeichnung { get; set; } = "";

    [JsonPropertyName("einheit")]
    public string Einheit { get; set; } = "";

    [JsonPropertyName("typische_menge")]
    [JsonConverter(typeof(FlexibleStringOrDoubleConverter))]
    public string? TypischeMenge { get; set; }

    [JsonPropertyName("marktpreis_chf")]
    public Marktpreis? MarktpreisChf { get; set; }
}

/// <summary>Akzeptiert sowohl String ("haltungslaenge") als auch Number (1) fuer typische_menge.</summary>
internal sealed class FlexibleStringOrDoubleConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

/// <summary>Akzeptiert vsa_zu_block_mapping mit "_kommentar"-Felder, ueberspringt Nicht-Array-Werte.</summary>
internal sealed class VsaMappingConverter : JsonConverter<Dictionary<string, IReadOnlyList<string>>>
{
    public override Dictionary<string, IReadOnlyList<string>> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (reader.TokenType != JsonTokenType.StartObject)
            return result;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var key = reader.GetString() ?? "";
            reader.Read();

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                        list.Add(reader.GetString() ?? "");
                }
                result[key] = list;
            }
            else
            {
                // Skip non-array values (z.B. _kommentar)
                reader.Skip();
            }
        }
        return result;
    }

    public override void Write(
        Utf8JsonWriter writer, Dictionary<string, IReadOnlyList<string>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (k, v) in value)
        {
            writer.WritePropertyName(k);
            JsonSerializer.Serialize(writer, v, options);
        }
        writer.WriteEndObject();
    }
}
