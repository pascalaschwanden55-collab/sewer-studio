namespace AuswertungPro.Next.Domain.Models;

public sealed record FieldDefinition(
    string Name,
    string Label,
    FieldType Type,
    IReadOnlyList<string>? ComboItems = null
);
