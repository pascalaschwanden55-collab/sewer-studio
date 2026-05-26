namespace AuswertungPro.Next.Application.Protocol;

public sealed class CompositeCodeCatalogProvider : ICodeCatalogProvider
{
    private readonly IReadOnlyList<ICodeCatalogProvider> _providers;
    private IReadOnlyList<CodeDefinition>? _overrideCodes;

    public CompositeCodeCatalogProvider(IReadOnlyList<ICodeCatalogProvider> providers)
    {
        _providers = providers.Where(p => p is not null).ToList();
    }

    public IReadOnlyList<CodeDefinition> GetAll()
        => _overrideCodes ?? MergeCodes(_providers.SelectMany(p => p.GetAll()));

    public bool TryGet(string code, out CodeDefinition def)
    {
        var normalized = NormalizeCode(code);
        foreach (var item in GetAll())
        {
            if (string.Equals(item.Code, normalized, StringComparison.OrdinalIgnoreCase))
            {
                def = CloneCode(item);
                return true;
            }
        }

        def = new CodeDefinition();
        return false;
    }

    public void Save(IReadOnlyList<CodeDefinition> codes)
        => _overrideCodes = MergeCodes(codes ?? Array.Empty<CodeDefinition>());

    public IReadOnlyList<string> AllowedCodes()
        => GetAll()
            .Where(c => c.IsSelectable && !c.IsObservedExtension)
            .Select(c => c.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
    {
        var list = codes ?? GetAll();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var row = 0;
        foreach (var code in list)
        {
            row++;
            if (string.IsNullOrWhiteSpace(code.Code))
                errors.Add($"Code fehlt (Eintrag #{row}).");
            else if (!seen.Add(NormalizeCode(code.Code)))
                errors.Add($"Duplikat-Code '{code.Code}'.");

            if (string.IsNullOrWhiteSpace(code.Title))
                errors.Add($"Title fehlt fuer Code '{code.Code}'.");
        }

        return errors;
    }

    public void Reload()
    {
        _overrideCodes = null;

        foreach (var provider in _providers)
        {
            switch (provider)
            {
                case XmlCodeCatalogProvider xml:
                    xml.Reload();
                    break;
                case JsonCodeCatalogProvider json:
                    json.Reload();
                    break;
                case ManifestCodeCatalogProvider manifest:
                    manifest.Reload();
                    break;
                case SourceDecoratingCodeCatalogProvider sourceDecorating:
                    sourceDecorating.Reload();
                    break;
                case CompositeCodeCatalogProvider composite:
                    composite.Reload();
                    break;
            }
        }
    }

    public IReadOnlyList<string> GetWarnings()
    {
        var warnings = new List<string>();
        foreach (var provider in _providers)
        {
            switch (provider)
            {
                case XmlCodeCatalogProvider xml:
                    warnings.AddRange(xml.LastLoadWarnings);
                    break;
                case JsonCodeCatalogProvider json:
                    warnings.AddRange(json.LastLoadWarnings);
                    break;
                case ManifestCodeCatalogProvider manifest:
                    warnings.AddRange(manifest.LastLoadWarnings);
                    warnings.AddRange(manifest.LastLoadErrors);
                    break;
                case SourceDecoratingCodeCatalogProvider sourceDecorating:
                    warnings.AddRange(sourceDecorating.GetWarnings());
                    break;
                case CompositeCodeCatalogProvider composite:
                    warnings.AddRange(composite.GetWarnings());
                    break;
            }
        }

        return warnings;
    }

    private static IReadOnlyList<CodeDefinition> MergeCodes(IEnumerable<CodeDefinition> codes)
    {
        var byCode = new Dictionary<string, CodeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in codes)
        {
            var clone = CloneCode(source);
            clone.Code = NormalizeCode(clone.Code);
            if (string.IsNullOrWhiteSpace(clone.Code))
                continue;

            byCode.TryAdd(clone.Code, clone);
        }

        return byCode.Values.OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static CodeDefinition CloneCode(CodeDefinition source)
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

    private static CodeParameter CloneParameter(CodeParameter source)
        => new()
        {
            Name = source.Name ?? string.Empty,
            DataKey = source.DataKey,
            Type = source.Type ?? "string",
            AllowedValues = source.AllowedValues?.ToList(),
            Unit = source.Unit,
            Required = source.Required
        };
}
