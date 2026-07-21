using System.Globalization;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using LegacyMeasureTemplate = AuswertungPro.Next.Domain.Models.Costs.MeasureTemplate;
using LegacyMeasureTemplates = AuswertungPro.Next.Domain.Models.Costs.MeasureTemplates;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Converts the former editor-specific measure-template format into the active catalog format.
/// </summary>
public static class LegacyMeasureTemplateConverter
{
    public static MeasureTemplateCatalog Convert(LegacyMeasureTemplates source)
    {
        var catalog = new MeasureTemplateCatalog { Version = Math.Max(1, source.SchemaVersion) };
        foreach (var legacyTemplate in source.Templates ?? new())
        {
            var template = ConvertTemplate(legacyTemplate);
            if (!string.IsNullOrWhiteSpace(template.Id))
                catalog.Measures.Add(template);
        }

        return catalog;
    }

    private static MeasureTemplate ConvertTemplate(LegacyMeasureTemplate source)
    {
        var template = new MeasureTemplate
        {
            Id = source.Id?.Trim() ?? "",
            Name = string.IsNullOrWhiteSpace(source.Name) ? source.Id?.Trim() ?? "" : source.Name.Trim()
        };

        foreach (var line in source.Lines ?? new())
        {
            if (string.IsNullOrWhiteSpace(line.ItemRef))
                continue;

            template.Lines.Add(new MeasureLineTemplate
            {
                Group = line.Group?.Trim() ?? "",
                ItemKey = line.ItemRef.Trim(),
                Enabled = true,
                DefaultQty = ParseQuantityOrDefault(line.Qty)
            });
        }

        return template;
    }

    private static decimal ParseQuantityOrDefault(JsonElement quantity)
    {
        if (quantity.ValueKind == JsonValueKind.Number && quantity.TryGetDecimal(out var number))
            return number;
        if (quantity.ValueKind == JsonValueKind.String)
            return ParseQuantityOrDefault(quantity.GetString());
        return 1m;
    }

    private static decimal ParseQuantityOrDefault(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 1m;

        var text = raw.Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity)
            ? quantity
            : 1m;
    }
}
