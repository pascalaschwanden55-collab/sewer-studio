using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models.Devis;

public sealed class DefektMassnahmeMapping
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("schadensCode")]
    public string SchadensCode { get; set; } = "";

    [JsonPropertyName("charakterisierung1")]
    public string? Charakterisierung1 { get; set; }

    [JsonPropertyName("charakterisierung2")]
    public string? Charakterisierung2 { get; set; }

    [JsonPropertyName("minZustandsklasse")]
    public int MinZustandsklasse { get; set; }

    [JsonPropertyName("maxZustandsklasse")]
    public int MaxZustandsklasse { get; set; } = 4;

    [JsonPropertyName("minDN")]
    public int? MinDN { get; set; }

    [JsonPropertyName("maxDN")]
    public int? MaxDN { get; set; }

    [JsonPropertyName("massnahme")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MassnahmenTyp Massnahme { get; set; }

    [JsonPropertyName("massnahmenBeschreibung")]
    public string MassnahmenBeschreibung { get; set; } = "";

    [JsonPropertyName("baumeisterPositionen")]
    public List<DevisPositionVorlage> BaumeisterPositionen { get; set; } = [];

    [JsonPropertyName("rohrleitungsbauPositionen")]
    public List<DevisPositionVorlage> RohrleitungsbauPositionen { get; set; } = [];

    [JsonPropertyName("prioritaet")]
    public int Prioritaet { get; set; } = 100;
}

public sealed class DevisPositionVorlage
{
    [JsonPropertyName("hauptposition")]
    public int Hauptposition { get; set; }

    [JsonPropertyName("unterposition")]
    public double Unterposition { get; set; }

    [JsonPropertyName("einzelposition")]
    public double Einzelposition { get; set; }

    [JsonPropertyName("bezeichnung")]
    public string Bezeichnung { get; set; } = "";

    [JsonPropertyName("einheit")]
    public string Einheit { get; set; } = "";

    [JsonPropertyName("referenzpreis")]
    public decimal Referenzpreis { get; set; }

    [JsonPropertyName("mengenFormel")]
    public string MengenFormel { get; set; } = "1";

    /// <summary>Submissions-Position (NPK/SIA-konform), z.B. "2.1.1", "600", "700".</summary>
    [JsonPropertyName("submissionPos")]
    public string? SubmissionPos { get; set; }

    /// <summary>Marktpreis-Bandbreite aus Submissions-Daten (Min/Median/Max in CHF/Einheit).</summary>
    [JsonPropertyName("marktpreis")]
    public Marktpreis? Marktpreis { get; set; }
}

/// <summary>Markt-Referenzpreis aus realen Submissions (Anbieter-Vergleich).</summary>
public sealed class Marktpreis
{
    [JsonPropertyName("min")]
    public decimal Min { get; set; }

    [JsonPropertyName("median")]
    public decimal Median { get; set; }

    [JsonPropertyName("max")]
    public decimal Max { get; set; }

    [JsonPropertyName("anzahl")]
    public int Anzahl { get; set; }

    [JsonPropertyName("quelle")]
    public string Quelle { get; set; } = "";
}

public sealed class DevisMappingConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("mappings")]
    public List<DefektMassnahmeMapping> Mappings { get; set; } = [];
}
