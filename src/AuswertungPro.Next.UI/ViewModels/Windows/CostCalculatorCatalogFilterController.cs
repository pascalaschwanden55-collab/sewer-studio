using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class CostCalculatorCatalogFilterController
{
    public List<CatalogItemOption> AllCatalogItems { get; } = new();
    public ObservableCollection<CatalogItemOption> FilteredCatalogItems { get; } = new();

    public void ReplaceItems(
        IEnumerable<CostCatalogItem> catalogItems,
        IEnumerable<MeasureTemplate> templates,
        string? searchText)
    {
        ArgumentNullException.ThrowIfNull(catalogItems);
        ArgumentNullException.ThrowIfNull(templates);

        var keyToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
            foreach (var line in template.Lines)
                if (!keyToGroup.ContainsKey(line.ItemKey))
                    keyToGroup[line.ItemKey] = line.Group;

        AllCatalogItems.Clear();
        AllCatalogItems.AddRange(
            catalogItems
                .Where(item => item.Active)
                .Select(item =>
                {
                    var group = keyToGroup.TryGetValue(item.Key, out var templateGroup)
                        ? templateGroup
                        : CatalogItemGrouping.DeriveGroupFromKey(item.Key);
                    return new CatalogItemOption(item.Key, group, $"[{group}]  {item.Name}  ({item.Unit})");
                })
                .OrderBy(item => CatalogItemGrouping.GetGroupOrder(item.Group))
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase));

        ApplyFilter(searchText);
    }

    public void ApplyFilter(string? searchText)
    {
        FilteredCatalogItems.Clear();
        var filter = searchText?.Trim() ?? "";
        foreach (var item in AllCatalogItems)
        {
            if (filter.Length == 0 || item.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                FilteredCatalogItems.Add(item);
        }
    }
}
