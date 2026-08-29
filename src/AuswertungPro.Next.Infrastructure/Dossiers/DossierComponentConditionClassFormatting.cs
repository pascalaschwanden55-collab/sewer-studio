using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Markiert die Zustandskuerzel in der erzeugten Dossier-Bauteilliste.
/// Die Farben stammen aus derselben fachlichen Palette wie die Haltungs- und
/// Schachtberichte. Andere Vorkommen wie ein Name "Z3-Weg" bleiben unberuehrt.
/// </summary>
internal static class DossierComponentConditionClassFormatting
{
    private static readonly IReadOnlyDictionary<string, string> ColorByClass =
        ExcelReportStyle.Zustandsklassen.ToDictionary(
            rule => rule.Wert,
            rule => rule.Farbe[^6..]);

    /// <summary>
    /// Erzeugt genau den Bereich des beim Listenbau bekannten Zustandskürzels.
    /// Freie Texte werden nicht nach ähnlich aussehenden Mustern durchsucht.
    /// </summary>
    public static DossierTextStyleRange? CreateRange(
        string? conditionClass,
        int tokenStart)
    {
        var key = (conditionClass ?? string.Empty).Trim();
        return tokenStart >= 0 && ColorByClass.TryGetValue(key, out var colorHex)
            ? new DossierTextStyleRange
            {
                Start = tokenStart,
                Length = 2,
                ColorHex = colorHex
            }
            : null;
    }
}
