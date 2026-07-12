using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class MeasureTemplateListItem
{
    public MeasureTemplate Template { get; }
    public string Id => Template.Id;
    public string Name => Template.Name;
    public bool Disabled => Template.Disabled;
    public string DisplayName => Disabled ? $"{Name} (deaktiviert)" : Name;

    public MeasureTemplateListItem(MeasureTemplate template)
    {
        Template = template;
    }
}

public sealed record CatalogItemOption(string Key, string Group, string DisplayName);
