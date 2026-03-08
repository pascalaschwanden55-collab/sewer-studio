using System.Text.Json;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Gemeinsam genutzte, gecachte JsonSerializerOptions-Instanzen.
/// JsonSerializerOptions ist teuer zu erstellen (interner Converter-/Type-Cache).
/// </summary>
public static class JsonDefaults
{
    /// <summary>PropertyNameCaseInsensitive = true</summary>
    public static JsonSerializerOptions CaseInsensitive { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>WriteIndented = true</summary>
    public static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true
    };

    /// <summary>WriteIndented = true + CamelCase naming</summary>
    public static JsonSerializerOptions IndentedCamel { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>PropertyNameCaseInsensitive + Comments + Trailing commas (fuer JSON-Config-Dateien)</summary>
    public static JsonSerializerOptions Lenient { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
