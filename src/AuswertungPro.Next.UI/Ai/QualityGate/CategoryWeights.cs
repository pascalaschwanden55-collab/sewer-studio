using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Per-damage-category weights for evidence fusion.
/// Stored in SQLite CategoryWeights table.
/// </summary>
public sealed class CategoryWeights
{
    public string Category { get; set; } = "default";
    public double WYolo { get; set; } = 0.10;
    public double WDino { get; set; } = 0.15;
    public double WSam { get; set; } = 0.10;
    public double WQwen { get; set; } = 0.15;
    public double WLlm { get; set; } = 0.20;
    public double WKb { get; set; } = 0.10;
    public double WKbAgreement { get; set; } = 0.10;
    public double WPlausibility { get; set; } = 0.10;
    public int ValidationCount { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Default weights (uniform-ish prior).</summary>
    public static CategoryWeights Default(string category = "default") => new() { Category = category };

    /// <summary>Returns weight array in canonical order for the optimizer.</summary>
    public double[] ToArray() => new[] { WYolo, WDino, WSam, WQwen, WLlm, WKb, WKbAgreement, WPlausibility };

    /// <summary>Sets weights from array in canonical order.</summary>
    public void FromArray(double[] w)
    {
        if (w.Length != 8) throw new ArgumentException("Expected 8 weights.");
        WYolo = w[0]; WDino = w[1]; WSam = w[2]; WQwen = w[3];
        WLlm = w[4]; WKb = w[5]; WKbAgreement = w[6]; WPlausibility = w[7];
    }

    /// <summary>Normalize weights to sum to 1.0.</summary>
    public void Normalize()
    {
        var sum = WYolo + WDino + WSam + WQwen + WLlm + WKb + WKbAgreement + WPlausibility;
        if (sum <= 0) return;
        WYolo /= sum; WDino /= sum; WSam /= sum; WQwen /= sum;
        WLlm /= sum; WKb /= sum; WKbAgreement /= sum; WPlausibility /= sum;
    }

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
    public static CategoryWeights FromJson(string json) =>
        JsonSerializer.Deserialize<CategoryWeights>(json, SerializerOptions) ?? Default();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
