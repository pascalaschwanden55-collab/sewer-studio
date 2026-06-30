using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>
/// Erstellt Tief-Kopien von <see cref="PositionGroup"/> und <see cref="PositionTemplate"/>.
/// Wird im Editor zum Laden und Zuruecksetzen auf Standardwerte verwendet,
/// damit Aenderungen nicht direkt auf dem geladenen Katalog-Objekt landen.
/// </summary>
public static class PositionTemplateCopier
{
    /// <summary>
    /// Erstellt eine vollstaendige Tief-Kopie einer <see cref="PositionGroup"/>
    /// inklusive aller enthaltenen <see cref="PositionTemplate"/>-Eintraege.
    /// </summary>
    public static PositionGroup DeepCopy(PositionGroup source) =>
        new()
        {
            Name = source.Name,
            Positions = new List<PositionTemplate>(source.Positions.Select(DeepCopy))
        };

    /// <summary>
    /// Erstellt eine vollstaendige Tief-Kopie einer einzelnen <see cref="PositionTemplate"/>.
    /// </summary>
    public static PositionTemplate DeepCopy(PositionTemplate source) =>
        new()
        {
            ItemKey = source.ItemKey,
            Enabled = source.Enabled,
            DefaultQty = source.DefaultQty,
            Name = source.Name,
            Unit = source.Unit,
            Price = source.Price,
            IsCustom = source.IsCustom
        };

    /// <summary>
    /// Erstellt Tief-Kopien aller Gruppen einer Sequenz.
    /// </summary>
    public static IEnumerable<PositionGroup> DeepCopyAll(IEnumerable<PositionGroup> groups) =>
        groups.Select(DeepCopy);
}
