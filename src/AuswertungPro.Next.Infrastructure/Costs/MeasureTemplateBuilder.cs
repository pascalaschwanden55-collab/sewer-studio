using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class MeasureTemplateBuilder
{
    private readonly PositionTemplateStore _positionStore = new();

    public MeasureTemplate BuildFromPositionTemplate(string measureId, string measureName, string[] selectedGroups, string? projectPath)
    {
        var positionCatalog = _positionStore.LoadMerged(projectPath);
        var lines = new List<MeasureLineTemplate>();

        foreach (var groupName in selectedGroups)
        {
            var group = positionCatalog.Groups.FirstOrDefault(g => 
                string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
            
            if (group is null) continue;

            foreach (var position in group.Positions)
            {
                lines.Add(new MeasureLineTemplate
                {
                    Group = group.Name,
                    ItemKey = position.ItemKey,
                    Enabled = position.Enabled,
                    DefaultQty = position.DefaultQty
                });
            }
        }

        return new MeasureTemplate
        {
            Id = measureId,
            Name = measureName,
            Lines = lines
        };
    }

    public List<string> GetAvailableGroups(string? projectPath)
    {
        var positionCatalog = _positionStore.LoadMerged(projectPath);
        return positionCatalog.Groups.Select(g => g.Name).ToList();
    }
}