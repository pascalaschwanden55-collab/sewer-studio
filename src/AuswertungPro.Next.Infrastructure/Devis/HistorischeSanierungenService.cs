using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Infrastructure.Devis;

/// <summary>
/// Liefert historische Sanierungs-Referenzen aus realen Auswertungen
/// (Buerglen 2024-2026, ~217 Haltungen, 27 DN/Material/Nutzungs-Profile).
///
/// Verwendung:
/// - DevisGenerator: Plausibilitaets-Check der berechneten Kosten gegen Median historischer Faelle
/// - AiSanierungOptimizationService: "Aehnliche historische Haltungen" als Empfehlungs-Begruendung
/// - PDF-Devis: Hinweis "Vergleichbare historische Sanierungen kosteten Median X CHF/m"
/// </summary>
public sealed class HistorischeSanierungenService
{
    private readonly string _path;
    private HistorischeSanierungenData? _data;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public HistorischeSanierungenService(string path)
    {
        _path = path;
    }

    public HistorischeSanierungenData LoadData()
    {
        if (_data is not null) return _data;
        if (!File.Exists(_path))
        {
            _data = new HistorischeSanierungenData();
            return _data;
        }
        var json = File.ReadAllText(_path);
        _data = JsonSerializer.Deserialize<HistorischeSanierungenData>(json, JsonOpts) ?? new HistorischeSanierungenData();
        return _data;
    }

    /// <summary>Setzt den Cache zurueck. Naechster LoadData liest die JSON neu.</summary>
    public void Invalidate() => _data = null;

    /// <summary>Pfad zur aktuellen JSON-Datei (fuer Anzeige/Diagnose).</summary>
    public string FilePath => _path;

    /// <summary>
    /// Findet das Profil mit aehnlichstem DN + Material + Nutzungsart.
    /// Gibt null zurueck wenn keine ausreichende Datenbasis (>= 3 Faelle) existiert.
    /// </summary>
    public ProfilAggregat? FindMatchingProfile(double? dnMm, string? material, string? nutzungsart)
    {
        var data = LoadData();
        var dnKlasse = ClassifyDn(dnMm);
        var matNorm = (material ?? "").Trim().ToUpperInvariant();
        var nutzNorm = (nutzungsart ?? "").Trim().ToUpperInvariant();

        // Strikt: exakter Match aller drei Kriterien
        var strict = data.ProfileAggregat
            .FirstOrDefault(p =>
                string.Equals(p.DnKlasse, dnKlasse, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Material, matNorm, StringComparison.OrdinalIgnoreCase)
                && p.Nutzungsart.ToUpperInvariant().Contains(nutzNorm.Substring(0, Math.Min(5, nutzNorm.Length))));
        if (strict is { AnzahlFaelle: >= 3 }) return strict;

        // Fallback: nur DN + Nutzungsart
        var loose = data.ProfileAggregat
            .Where(p => string.Equals(p.DnKlasse, dnKlasse, StringComparison.OrdinalIgnoreCase))
            .Where(p => string.IsNullOrEmpty(nutzNorm) ||
                        p.Nutzungsart.ToUpperInvariant().Contains(nutzNorm.Substring(0, Math.Min(5, nutzNorm.Length))))
            .OrderByDescending(p => p.AnzahlFaelle)
            .FirstOrDefault();
        return loose is { AnzahlFaelle: >= 3 } ? loose : null;
    }

    private static string ClassifyDn(double? dn)
    {
        if (dn is null) return "?";
        return dn switch
        {
            <= 200 => "≤200",
            <= 300 => "DN250-300",
            <= 500 => "DN400+",
            _ => ">DN500",
        };
    }
}

// ── Datenmodell ─────────────────────────────────────────────────────────

public sealed class HistorischeSanierungenData
{
    [JsonPropertyName("_meta")]
    public HistorischeMeta? Meta { get; set; }

    [JsonPropertyName("haltungen")]
    public List<HistorischeHaltung> Haltungen { get; set; } = new();

    [JsonPropertyName("profile_aggregat")]
    public List<ProfilAggregat> ProfileAggregat { get; set; } = new();
}

public sealed class HistorischeMeta
{
    [JsonPropertyName("quelle")]
    public string Quelle { get; set; } = "";
    [JsonPropertyName("stand")]
    public string Stand { get; set; } = "";
    [JsonPropertyName("anzahl_haltungen")]
    public int AnzahlHaltungen { get; set; }
    [JsonPropertyName("zonen")]
    public List<string> Zonen { get; set; } = new();
}

public sealed class HistorischeHaltung
{
    [JsonPropertyName("zone")]
    public string Zone { get; set; } = "";
    [JsonPropertyName("haltung")]
    public string Haltung { get; set; } = "";
    [JsonPropertyName("dn_mm")]
    public double? DnMm { get; set; }
    [JsonPropertyName("material")]
    public string Material { get; set; } = "";
    [JsonPropertyName("nutzungsart")]
    public string Nutzungsart { get; set; } = "";
    [JsonPropertyName("laenge_m")]
    public double? LaengeM { get; set; }
    [JsonPropertyName("zustandsklasse")]
    public double? Zustandsklasse { get; set; }
    [JsonPropertyName("massnahme_text")]
    public string MassnahmeText { get; set; } = "";
    [JsonPropertyName("kosten_chf")]
    public double? KostenChf { get; set; }
}

public sealed class ProfilAggregat
{
    [JsonPropertyName("dn_klasse")]
    public string DnKlasse { get; set; } = "";
    [JsonPropertyName("material")]
    public string Material { get; set; } = "";
    [JsonPropertyName("nutzungsart")]
    public string Nutzungsart { get; set; } = "";
    [JsonPropertyName("anzahl_faelle")]
    public int AnzahlFaelle { get; set; }
    [JsonPropertyName("haltungslaenge_median_m")]
    public double? HaltungslaengeMedianM { get; set; }
    [JsonPropertyName("kosten_pro_m_median_chf")]
    public double? KostenProMMedianChf { get; set; }
    [JsonPropertyName("kosten_pro_m_min_chf")]
    public double? KostenProMMinChf { get; set; }
    [JsonPropertyName("kosten_pro_m_max_chf")]
    public double? KostenProMMaxChf { get; set; }
    [JsonPropertyName("kosten_pro_haltung_median_chf")]
    public double? KostenProHaltungMedianChf { get; set; }
    [JsonPropertyName("anschluss_je_haltung_median")]
    public double? AnschlussJeHaltungMedian { get; set; }
    [JsonPropertyName("typische_massnahmen")]
    public List<string> TypischeMassnahmen { get; set; } = new();
}
