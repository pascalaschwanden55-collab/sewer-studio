namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Gemeinsamer statischer Helfer fuer das Field-by-Field-Cloning von CodeDefinition und CodeParameter.
/// Alle Provider delegieren hierher; kein Duplikat mehr in den einzelnen Klassen.
/// </summary>
internal static class CodeDefinitionCloning
{
    /// <summary>
    /// Erstellt eine tiefe Kopie einer <see cref="CodeDefinition"/> mit normalisierten Leer-Fallbacks.
    /// </summary>
    internal static CodeDefinition CloneCode(CodeDefinition source)
        => new()
        {
            Code = source.Code ?? string.Empty,
            Title = source.Title ?? string.Empty,
            CanonicalCode = source.CanonicalCode,
            Source = source.Source,
            IsObservedExtension = source.IsObservedExtension,
            IsSelectable = source.IsSelectable,
            StandardAnnotation = source.StandardAnnotation,
            Group = source.Group ?? "Unbekannt",
            Description = source.Description,
            CategoryPath = (source.CategoryPath ?? new List<string>()).ToList(),
            Parameters = (source.Parameters ?? new List<CodeParameter>()).Select(CloneParameter).ToList(),
            Examples = (source.Examples ?? new List<string>()).ToList(),
            RequiresRange = source.RequiresRange,
            RangeThresholdM = source.RangeThresholdM,
            RangeThresholdText = source.RangeThresholdText
        };

    /// <summary>
    /// Erstellt eine Kopie eines <see cref="CodeParameter"/> mit Trim und Leer-Fallbacks.
    /// DataKey und Unit werden auf null gesetzt wenn leer/whitespace.
    /// Type faellt auf "string" zurueck wenn leer/whitespace.
    /// </summary>
    internal static CodeParameter CloneParameter(CodeParameter source)
        => new()
        {
            Name = source.Name ?? string.Empty,
            DataKey = string.IsNullOrWhiteSpace(source.DataKey) ? null : source.DataKey.Trim(),
            Type = string.IsNullOrWhiteSpace(source.Type) ? "string" : source.Type.Trim(),
            AllowedValues = source.AllowedValues?.ToList(),
            Unit = string.IsNullOrWhiteSpace(source.Unit) ? null : source.Unit.Trim(),
            Required = source.Required
        };
}
