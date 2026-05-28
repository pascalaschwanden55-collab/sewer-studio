using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Vsa.Classification;

public sealed class VsaClassificationRuleSet
{
    public int SchemaVersion { get; set; }
    public string Source { get; set; } = "";
    public string AssetKind { get; set; } = "";
    public List<VsaClassificationRule> Rules { get; set; } = new();

    public static VsaClassificationRuleSet LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VsaClassificationRuleSet>(
            json,
            Application.Common.JsonDefaults.CaseInsensitive)
            ?? throw new InvalidDataException($"VSA-Regelmodell konnte nicht geladen werden: {path}");
    }
}

public sealed class VsaClassificationRule
{
    public string Id { get; set; } = "";
    public string Code { get; set; } = "";
    public string CodeMatch { get; set; } = "exact";
    public List<string> Ch1 { get; set; } = new();
    public List<string> Ch2 { get; set; } = new();
    public string? Requirement { get; set; }
    public string Parameter { get; set; } = "none";
    public string Unit { get; set; } = "none";
    public VsaClassificationScope Scope { get; set; } = new();
    public VsaClassificationDefinition Classification { get; set; } = new();
    public string Status { get; set; } = "ok";
    public string SourceRef { get; set; } = "";
    public List<string> Notes { get; set; } = new();
}

public sealed class VsaClassificationScope
{
    public string PipeFlexibility { get; set; } = "any";
    public List<string> Areas { get; set; } = new();
}

public sealed class VsaClassificationDefinition
{
    public string Mode { get; set; } = "";
    public int? Ez { get; set; }
    public List<VsaClassificationRange> Ranges { get; set; } = new();
    public string? Reason { get; set; }
}

public sealed class VsaClassificationRange
{
    public int Ez { get; set; }
    public double? MinInclusive { get; set; }
    public double? MaxExclusive { get; set; }
}
