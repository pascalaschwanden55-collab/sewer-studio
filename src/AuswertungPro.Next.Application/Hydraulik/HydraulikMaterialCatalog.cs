namespace AuswertungPro.Next.Application.Hydraulik;

public sealed record MaterialOption(string Key, string Label, double KbNeu, double KbAlt)
{
    public override string ToString() => Label;
}

public static class HydraulikMaterialCatalog
{
    public static IReadOnlyList<MaterialOption> Materials { get; } =
    [
        new("Beton", "Beton", 0.0005, 0.0015),
        new("Steinzeug", "Steinzeug", 0.0003, 0.001),
        new("PVC/PE", "Kunststoff (PVC/PE)", 0.0002, 0.0005),
        new("GFK", "GFK", 0.0003, 0.0008),
        new("Guss", "Gusseisen", 0.001, 0.003),
    ];

    public static MaterialOption Resolve(string? recordMaterial, string? settingsMaterialKey)
    {
        var material = Materials.FirstOrDefault(m =>
                string.Equals(m.Key, settingsMaterialKey, StringComparison.OrdinalIgnoreCase))
            ?? Materials[0];

        return ResolveRecordMaterial(recordMaterial, material)!;
    }

    public static MaterialOption? ResolveRecordMaterial(
        string? recordMaterial,
        MaterialOption? fallbackMaterial)
    {
        if (string.IsNullOrWhiteSpace(recordMaterial))
            return fallbackMaterial;

        return Materials.FirstOrDefault(m =>
                m.Label.Contains(recordMaterial, StringComparison.OrdinalIgnoreCase)
                || m.Key.Equals(recordMaterial, StringComparison.OrdinalIgnoreCase))
            ?? fallbackMaterial;
    }
}
