using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Fester, aus den offiziellen ILI-Dateien erzeugter Vertrag. Keine geratenen WebGIS-Codes.</summary>
public static class DssExportSchema
{
    public const string Modell = "DSS_2020_1_LV95";
    public const string Fach = Modell + ".Siedlungsentwaesserung";
    public const string Basis = "SIA405_Base_Abwasser_1_LV95";
    public const string Verwaltung = Basis + ".Administration";
    private static readonly Lazy<Schema> Daten = new(() =>
    {
        using var stream = typeof(DssExportSchema).Assembly.GetManifestResourceStream("Dss.ExportSchema.json")!;
        return JsonSerializer.Deserialize<Schema>(stream)!;
    });
    public static IReadOnlyDictionary<string, DssAttribut>? Felder(string klasse) => Daten.Value.Classes.GetValueOrDefault(klasse);
    public static string? Normalisiere(string klasse, string feld, string text)
    {
        var typ = Felder(klasse)?.GetValueOrDefault(feld);
        if (typ is null || typ.Kind == "Structure") throw new InvalidOperationException($"DSS: {klasse}.{feld} ist kein freigegebenes Sachfeld.");
        if (text.Length == 0) return null;
        string? result = typ.Kind switch
        {
            "Text" => (typ.MaxLength == 0 || text.Length <= typ.MaxLength) && !text.Any(c => c < 32 && c is not ('\r' or '\n' or '\t')) ? text : null,
            "Enum" => typ.Values.Contains(text, StringComparer.Ordinal) ? text : EnumText(typ, text),
            "Date" => Datum(text),
            "Number" => Zahl(typ, text),
            _ => null
        };
        return result ?? throw new InvalidOperationException($"DSS: {klasse}.{feld}: ungültiger Wert „{text}“. Bitte in der Objektakte korrigieren.");
    }
    private static string? EnumText(DssAttribut typ, string text)
    {
        // Ausschliesslich eindeutige Schreibvarianten des Normtextes; numerische Dropdown-Indizes sind keine INTERLIS-Werte.
        static string Key(string s) => Regex.Replace(s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss"), @"[\s_.-]", "");
        var kandidaten = typ.Values.Where(v => Key(v) == Key(text)).ToArray();
        return kandidaten.Length == 1 ? kandidaten[0] : null;
    }
    private static string? Datum(string text) => DateOnly.TryParseExact(text.Trim(), ["yyyyMMdd", "yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
        ? d.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : null;
    private static string? Zahl(DssAttribut typ, string text)
    {
        var clean = text.Trim().Replace(',', '.');
        if (!Regex.IsMatch(clean, @"^-?\d+(?:\.\d+)?$") || !decimal.TryParse(clean, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
            || n < decimal.Parse(typ.Minimum, CultureInfo.InvariantCulture) || n > decimal.Parse(typ.Maximum, CultureInfo.InvariantCulture)
            || decimal.Round(n, typ.Decimals) != n) return null;
        return n.ToString(typ.Decimals == 0 ? "0" : "0." + new string('0', typ.Decimals), CultureInfo.InvariantCulture);
    }
    private sealed class Schema { public Dictionary<string, Dictionary<string, DssAttribut>> Classes { get; set; } = new(); }
}

public sealed class DssAttribut
{
    public string Kind { get; set; } = "";
    public bool Required { get; set; }
    public string[] Values { get; set; } = [];
    public int MaxLength { get; set; }
    public string Minimum { get; set; } = "0";
    public string Maximum { get; set; } = "0";
    public int Decimals { get; set; }
}
