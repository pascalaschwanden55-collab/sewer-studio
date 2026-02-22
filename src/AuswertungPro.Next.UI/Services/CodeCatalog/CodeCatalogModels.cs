
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.UI.Services.CodeCatalog;

public sealed class CodeCatalog
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("codes")]
    public List<CodeDef> Codes { get; set; } = new();
}

public sealed class CodeDef
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("categoryPath")]
    public List<string> CategoryPath { get; set; } = new();

    [JsonPropertyName("isStretchDamage")]
    public bool IsStretchDamage { get; set; }

    [JsonPropertyName("parameters")]
    public List<ParamDef> Parameters { get; set; } = new();
}

public sealed class ParamDef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    // "string|double|int|bool|clock"
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

public sealed class CodeCatalogTreeNode
{
    public string Name { get; }
    public List<CodeCatalogTreeNode> Children { get; } = new();
    public List<CodeDef> Codes { get; } = new();

    public CodeCatalogTreeNode(string name) => Name = name;

    public CodeCatalogTreeNode GetOrAddChild(string name)
    {
        var existing = Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;
        var created = new CodeCatalogTreeNode(name);
        Children.Add(created);
        return created;
    }
}
