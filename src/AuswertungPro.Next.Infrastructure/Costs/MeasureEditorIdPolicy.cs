using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Kapselt die ID-Generierungsregeln des MeasureTemplateEditor:
/// Template-IDs ("template_{N}") und Katalog-Positions-IDs ("neu_{N}").
/// Kollisionsfreie IDs werden per Inkrement-Schleife erzeugt.
/// </summary>
public static class MeasureEditorIdPolicy
{
    /// <summary>
    /// Erzeugt eine neue Template-ID im Format "template_{templateCount + 1}".
    /// Die ID ist bewusst nicht kollisionssicher, da Template-IDs vom Nutzer bearbeitet werden.
    /// </summary>
    public static string NewTemplateId(int templateCount)
        => $"template_{templateCount + 1}";

    /// <summary>
    /// Erzeugt eine kollisionsfreie Katalog-Positions-ID im Format "neu_{N}".
    /// <paramref name="existingIds"/> enthält alle bereits belegten IDs (Gross-/Kleinschreibung ignoriert).
    /// </summary>
    public static string NewCatalogItemId(IReadOnlyCollection<string> existingIds)
    {
        var index = existingIds.Count + 1;
        while (true)
        {
            var candidate = $"neu_{index}";
            if (!existingIds.Any(id => string.Equals(id, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            index++;
        }
    }
}
