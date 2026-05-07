using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Sanierung;

/// <summary>
/// Vom Benutzer im UI gepflegte Hard-Constraint-Regel fuer die Sanierungs-KI.
/// Format: WENN [Bedingungen alle erfuellt] DANN [exkludiere diese Verfahren] WEIL [Begruendung].
///
/// Beispiel:
///   Bedingung: material_contains="GFK", bend_degrees_min=15
///   Aktion:    exclude_procedure_ids=["cipp_inliner"]
///   Reason:    "GFK-Liner ist nicht bogengaengig"
///
/// Benutzer pflegt diese Regeln im Settings-UI ohne Programmierkenntnisse.
/// Engine kombiniert User-Regeln mit Default-Regeln (Code-Hardcoded).
/// </summary>
public sealed class SanierungUserRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>True = aktiv, False = vom Benutzer deaktiviert (bleibt aber in Liste).</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("conditions")]
    public RuleConditions Conditions { get; set; } = new();

    /// <summary>Liste der Verfahrens-IDs die ausgeschlossen werden sollen.</summary>
    [JsonPropertyName("exclude_procedure_ids")]
    public List<string> ExcludeProcedureIds { get; set; } = new();

    /// <summary>Begruendung die im KI-Prompt + UI angezeigt wird.</summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>
/// Bedingungen einer Regel. Alle gesetzten Felder muessen erfuellt sein (UND-Verknuepfung).
/// Null/leer = Feld nicht geprueft.
/// </summary>
public sealed class RuleConditions
{
    /// <summary>Material enthaelt diese Zeichenkette (case-insensitive). z.B. "GFK", "Steinzeug", "Asbest"</summary>
    [JsonPropertyName("material_contains")]
    public string? MaterialContains { get; set; }

    /// <summary>Material enthaelt KEINE dieser Zeichenketten (Negation).</summary>
    [JsonPropertyName("material_not_contains")]
    public string? MaterialNotContains { get; set; }

    /// <summary>DN >= dieser Wert.</summary>
    [JsonPropertyName("dn_min")]
    public int? DnMin { get; set; }

    /// <summary>DN <= dieser Wert.</summary>
    [JsonPropertyName("dn_max")]
    public int? DnMax { get; set; }

    /// <summary>Bogenwinkel >= dieser Wert (aus VSA BCC-Codes Q1).</summary>
    [JsonPropertyName("bend_degrees_min")]
    public double? BendDegreesMin { get; set; }

    /// <summary>Vorhanden sein muss eine Schadensgruppe aus dieser Liste.
    /// Werte: cracks, joints, infiltration, exfiltration, laterals, obstructions, deformation, breaks, corrosion</summary>
    [JsonPropertyName("damage_group_any_of")]
    public List<string>? DamageGroupAnyOf { get; set; }

    /// <summary>VSA-Code muss in den Findings enthalten sein (z.B. "BAC" fuer Einsturz).</summary>
    [JsonPropertyName("vsa_code_starts_with")]
    public string? VsaCodeStartsWith { get; set; }

    /// <summary>Zustandsklasse (VSA 2023 Skala: 0=schlecht, 4=gut). Match wenn ZK <= dieser Wert (= mindestens dieser Schweregrad).</summary>
    [JsonPropertyName("zustandsklasse_max")]
    public int? ZustandsklasseMax { get; set; }

    /// <summary>True = Grundwasser vorhanden, False = nicht, null = egal.</summary>
    [JsonPropertyName("groundwater")]
    public bool? Groundwater { get; set; }

    /// <summary>Nutzungsart enthaelt (case-insensitive).</summary>
    [JsonPropertyName("nutzungsart_contains")]
    public string? NutzungsartContains { get; set; }
}

/// <summary>Container fuer JSON-Persistierung.</summary>
public sealed class SanierungUserRulesFile
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    [JsonPropertyName("rules")]
    public List<SanierungUserRule> Rules { get; set; } = new();
}
