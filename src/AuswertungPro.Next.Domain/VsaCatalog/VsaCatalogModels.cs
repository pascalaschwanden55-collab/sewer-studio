using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.VsaCatalog;

public sealed record GroupDef(string Label, string Color, string Icon, Dictionary<string, VsaCodeDef> Codes);

public sealed record VsaCodeDef
{
    public string Label { get; init; } = "";
    public string? FinalCode { get; init; }
    public bool IsSteuer { get; init; }
    public string? Note { get; init; }
    public string? Warn { get; init; }
    public string? Source { get; init; }
    public string? CanonicalCode { get; init; }
    public string? StandardAnnotation { get; init; }
    public bool XPrefix { get; init; }
    public Dictionary<string, CharDef>? Char1 { get; init; }
    public Dictionary<string, string>? Char2 { get; init; }
    public Dictionary<string, Dictionary<string, string>>? Char2PerChar1 { get; init; }
    public Dictionary<string, HashSet<string>>? Invalid { get; init; }
    public bool AllValid { get; init; }
}

public sealed record CharDef
{
    public string Label { get; init; } = "";
    public Dictionary<string, string>? Char2 { get; init; }
}

public sealed record QuantRule
{
    public QuantField? Q1 { get; init; }
    public Dictionary<string, QuantField?>? Q1PerChar1 { get; init; }
    public QuantField? Q2 { get; init; }
}

public sealed record QuantField
{
    public string Pflicht { get; init; } = "O";
    public string? Einheit { get; init; }
    public string? Label { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string? Hint { get; init; }
}

public sealed record ClockRule
{
    public string Mode { get; init; } = "range";
    public string? Hint { get; init; }
}
